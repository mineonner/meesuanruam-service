using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using meesuanruam_service.DTO;
using meesuanruam_service.DTO.table;
using meesuanruam_service.model.respone;
using meesuanruam_service.services;

namespace meesuanruam_service.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/Upload")]
    [ApiController]
    public class UploadController : ControllerBase
    {
        private readonly meeDB _dbContext;
        private readonly ILogger<UploadController> _logger;
        private readonly OrgUnitService _orgUnitService;
        private readonly FileStorageService _storage;
        private readonly HashService _hashService;
        private readonly string _keyProject;

        public UploadController(ILogger<UploadController> logger, meeDB context, IConfiguration config,
                                OrgUnitService orgUnitService, FileStorageService storage)
        {
            _logger = logger;
            _dbContext = context;
            _orgUnitService = orgUnitService;
            _storage = storage;
            _hashService = new HashService(config);
            _keyProject = config["ProjectCode:AesKey"];
        }

        /// <summary>
        /// รับไฟล์แนบของเรื่องร้องเรียน/ความคิดเห็น
        /// เปิดสาธารณะเพราะผู้ร้องไม่ได้ล็อกอิน จึงกันด้วย Origin + ต้องมีเรื่องนั้นอยู่จริง
        /// และอัปได้ครั้งเดียวต่อเรื่อง
        ///
        /// แถวใน FILE ถูกเขียนที่นี่ ไม่ใช่ที่ saveReport/saveComment
        /// ของเดิมเขียนแถวก่อนไฟล์ลงดิสก์ พออัปพลาดจึงเหลือแถวชี้ไฟล์ที่ไม่มีอยู่
        /// </summary>
        [HttpPost]
        [Route("saveImages")]
        public async Task<IActionResult> saveImages([FromForm] Files files)
        {
            DataRespone res = new DataRespone();
            try
            {
                string orgUnitCode = _orgUnitService.ResolveFromOrigin(HttpContext.Request.Headers["Origin"].ToString());

                if (!FileStorageService.TryParsePathFile(files.pathFile, out string folder, out string code))
                {
                    return Reject(res, $"pathFile '{files.pathFile}' ไม่ถูกรูปแบบ");
                }

                if (files.formFiles == null || files.formFiles.Count == 0)
                {
                    return Reject(res, "ไม่มีไฟล์แนบมาด้วย");
                }

                if (files.formFiles.Count > FileStorageService.MaxFilesPerRecord)
                {
                    return Reject(res, $"แนบได้ไม่เกิน {FileStorageService.MaxFilesPerRecord} ไฟล์ต่อหนึ่งเรื่อง");
                }

                if (!RecordBelongsToOrg(folder, code, orgUnitCode))
                {
                    return Reject(res, $"ไม่พบเรื่อง '{code}' ของ อปท. {orgUnitCode}");
                }

                // อัปได้ครั้งเดียวต่อเรื่อง กันไม่ให้ใครยิงไฟล์ใส่รหัสเดิมซ้ำๆ จนดิสก์เต็ม
                if (_dbContext.file.Any(o => o.code_reference == code))
                {
                    return Reject(res, $"เรื่อง '{code}' มีไฟล์แนบอยู่แล้ว");
                }

                foreach (IFormFile file in files.formFiles)
                {
                    if (file.Length > FileStorageService.MaxBytesPerFile)
                    {
                        return Reject(res, $"ไฟล์ '{file.FileName}' ใหญ่เกิน 10MB");
                    }

                    if (!FileStorageService.IsAllowedExtension(file.FileName))
                    {
                        return Reject(res, $"ไม่รองรับไฟล์ '{file.FileName}' รองรับเฉพาะ {FileStorageService.AllowedExtensionList}");
                    }
                }

                foreach (IFormFile file in files.formFiles)
                {
                    string? safeName = FileStorageService.SanitizeFileName(file.FileName);
                    if (safeName == null)
                    {
                        return Reject(res, $"ชื่อไฟล์ '{file.FileName}' ใช้ไม่ได้");
                    }

                    string relativePath = FileStorageService.BuildRelativePath(orgUnitCode, folder, code, safeName);

                    using (Stream stream = file.OpenReadStream())
                    {
                        await _storage.SaveAsync(relativePath, stream);
                    }

                    _dbContext.file.Add(new FILE()
                    {
                        code_reference = code,
                        file_path = relativePath,
                        name = safeName,
                        type = file.ContentType,
                        size = file.Length,
                    });
                }

                _dbContext.SaveChanges();

                res.status = "success";
                return Ok(res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Path} ล้มเหลว", HttpContext.Request.Path);
                res.status = "error";
                res.message = "ระบบขัดข้องชั่วคราว อยู่ระหว่างดำเนินการแก้ไข";
                return BadRequest(res);
            }
        }

        /// <summary>
        /// ส่งไฟล์แนบกลับ สิทธิ์มาจาก token อายุสั้นที่ฝังอยู่ใน URL
        /// ไม่ใช้ [Authorize] เพราะ frontend เปิดไฟล์ด้วย &lt;a href&gt; ซึ่งแนบ header ไม่ได้
        /// และห้ามใช้ UseStaticFiles เด็ดขาด เพราะจะทำให้ไฟล์เปิดสาธารณะเหมือน blob เดิม
        /// </summary>
        [HttpGet]
        [Route("file")]
        public IActionResult file([FromQuery] string? t)
        {
            try
            {
                var claims = _hashService.readFileToken(t ?? string.Empty);
                if (claims == null)
                {
                    return StatusCode(401);
                }

                (string? filePath, string? fileName) = claims.Value.kind == "project_file"
                    ? _dbContext.project_file.Where(o => o.id == claims.Value.fileId)
                          .Select(o => new ValueTuple<string?, string?>(o.file_path, o.name)).FirstOrDefault()
                    : _dbContext.file.Where(o => o.id == claims.Value.fileId)
                          .Select(o => new ValueTuple<string?, string?>(o.file_path, o.name)).FirstOrDefault();

                if (filePath == null)
                {
                    return NotFound();
                }

                // token ผูกกับ อปท. ตอนออก ถ้าแถวถูกย้ายเจ้าของภายหลังจะไม่ตรงกัน
                if (!filePath.StartsWith(claims.Value.orgUnitCode + "/", StringComparison.Ordinal))
                {
                    return StatusCode(403);
                }

                if (!_storage.TryGetFullPath(filePath, out string fullPath))
                {
                    _logger.LogWarning("แถว {Kind} id={Id} ชี้ไฟล์ที่ไม่มีอยู่: {FilePath}", claims.Value.kind, claims.Value.fileId, filePath);
                    return NotFound();
                }

                new FileExtensionContentTypeProvider().TryGetContentType(fileName ?? string.Empty, out string? contentType);
                return PhysicalFile(fullPath, contentType ?? "application/octet-stream", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Path} ล้มเหลว", HttpContext.Request.Path);
                return StatusCode(500);
            }
        }

        /// <summary>
        /// ไฟล์แนบของแบบประเมิน ผูกกับตัวชี้วัดรายข้อผ่าน measures_prefix
        /// ต่างจาก saveImages ตรงที่ผู้ใช้ต้องล็อกอิน จึงอ่าน อปท. จาก claim ไม่ใช่ Origin
        /// </summary>
        [HttpPost]
        [Route("saveProjectFiles")]
        [Authorize]
        public async Task<IActionResult> saveProjectFiles([FromForm] ProjectFiles body)
        {
            DataRespone res = new DataRespone();
            try
            {
                string orgUnitCode = CurrentOrgUnit();

                string projectCode;
                try
                {
                    projectCode = HashService.AesDecryptString(_keyProject, body.code ?? string.Empty);
                }
                catch
                {
                    return Reject(res, "รหัสโครงการไม่ถูกต้อง");
                }

                if (!FileStorageService.IsValidProjectCode(projectCode))
                {
                    return Reject(res, "รหัสโครงการไม่ถูกต้อง");
                }

                if (!FileStorageService.IsValidMeasuresPrefix(body.measuresPrefix))
                {
                    return Reject(res, $"measuresPrefix '{body.measuresPrefix}' ไม่ถูกรูปแบบ");
                }

                if (body.formFiles == null || body.formFiles.Count == 0)
                {
                    return Reject(res, "ไม่มีไฟล์แนบมาด้วย");
                }

                if (!_dbContext.project.Any(o => o.code == projectCode && o.org_unit_code == orgUnitCode))
                {
                    return Reject(res, $"ไม่พบโครงการ '{projectCode}' ของ อปท. {orgUnitCode}");
                }

                int existing = _dbContext.project_file.Count(o => o.project_code == projectCode && o.measures_prefix == body.measuresPrefix);
                if (existing + body.formFiles.Count > FileStorageService.MaxFilesPerRecord)
                {
                    return Reject(res, $"แนบได้ไม่เกิน {FileStorageService.MaxFilesPerRecord} ไฟล์ต่อหนึ่งตัวชี้วัด");
                }

                foreach (IFormFile file in body.formFiles)
                {
                    if (file.Length > FileStorageService.MaxBytesPerFile)
                    {
                        return Reject(res, $"ไฟล์ '{file.FileName}' ใหญ่เกิน 10MB");
                    }

                    if (!FileStorageService.IsAllowedExtension(file.FileName))
                    {
                        return Reject(res, $"ไม่รองรับไฟล์ '{file.FileName}' รองรับเฉพาะ {FileStorageService.AllowedExtensionList}");
                    }
                }

                foreach (IFormFile file in body.formFiles)
                {
                    string? safeName = FileStorageService.SanitizeFileName(file.FileName);
                    if (safeName == null)
                    {
                        return Reject(res, $"ชื่อไฟล์ '{file.FileName}' ใช้ไม่ได้");
                    }

                    string relativePath = FileStorageService.BuildProjectRelativePath(orgUnitCode, projectCode, body.measuresPrefix!, safeName);

                    using (Stream stream = file.OpenReadStream())
                    {
                        await _storage.SaveAsync(relativePath, stream);
                    }

                    // แนบชื่อซ้ำในตัวชี้วัดเดิม = แทนที่ของเดิม ไม่สร้างแถวซ้อน
                    PROJECT_FILE? dup = _dbContext.project_file.FirstOrDefault(
                        o => o.project_code == projectCode && o.measures_prefix == body.measuresPrefix && o.name == safeName);

                    if (dup != null)
                    {
                        dup.file_path = relativePath;
                        dup.type = file.ContentType;
                        dup.size = file.Length;
                    }
                    else
                    {
                        _dbContext.project_file.Add(new PROJECT_FILE()
                        {
                            project_code = projectCode,
                            measures_prefix = body.measuresPrefix!,
                            file_path = relativePath,
                            name = safeName,
                            type = file.ContentType,
                            size = file.Length,
                        });
                    }
                }

                _dbContext.SaveChanges();

                res.status = "success";
                return Ok(res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Path} ล้มเหลว", HttpContext.Request.Path);
                res.status = "error";
                res.message = "ระบบขัดข้องชั่วคราว อยู่ระหว่างดำเนินการแก้ไข";
                return BadRequest(res);
            }
        }

        [HttpPost]
        [Route("deleteProjectFile")]
        [Authorize]
        public IActionResult deleteProjectFile([FromQuery] long id)
        {
            DataRespone res = new DataRespone();
            try
            {
                string orgUnitCode = CurrentOrgUnit();

                PROJECT_FILE? row = _dbContext.project_file.FirstOrDefault(o => o.id == id);
                if (row == null)
                {
                    return NotFound();
                }

                if (!row.file_path.StartsWith(orgUnitCode + "/", StringComparison.Ordinal))
                {
                    return StatusCode(403);
                }

                _storage.Delete(row.file_path);
                _dbContext.project_file.Remove(row);
                _dbContext.SaveChanges();

                res.status = "success";
                return Ok(res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Path} ล้มเหลว", HttpContext.Request.Path);
                return StatusCode(500);
            }
        }

        private string CurrentOrgUnit() =>
            User.FindFirst("org_unit_code")?.Value
            ?? throw new InvalidOperationException("token ไม่มี org_unit_code กรุณาเข้าสู่ระบบใหม่");

        private bool RecordBelongsToOrg(string folder, string code, string orgUnitCode) =>
            folder == "report"
                ? _dbContext.report.Any(o => o.report_code == code && o.org_unit_code == orgUnitCode)
                : _dbContext.comment.Any(o => o.comment_code == code && o.org_unit_code == orgUnitCode);

        private IActionResult Reject(DataRespone res, string reason)
        {
            _logger.LogWarning("ปฏิเสธการอัปโหลด: {Reason}", reason);
            res.status = "error";
            res.message = reason;
            return BadRequest(res);
        }
    }

    public class ProjectFiles
    {
        /// <summary>รหัสโครงการที่เข้ารหัสแล้ว ตัวเดียวกับที่ getProjectList ส่งออกไป</summary>
        public string? code { get; set; }
        public string? measuresPrefix { get; set; }
        public List<IFormFile> formFiles { get; set; }
    }

    public class Files
    {
        public string pathFile { get; set; }
        public List<IFormFile> formFiles { get; set; }
    }
}
