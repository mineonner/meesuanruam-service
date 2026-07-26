using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using meesuanruam_service.DTO;
using meesuanruam_service.DTO.table;
using meesuanruam_service.model.request;
using meesuanruam_service.model.respone;
using meesuanruam_service.services;

namespace meesuanruam_service.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/meesuanruamAd")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private HashService _hashService;
        private readonly DTO.meeDB _dbContext;
        private readonly ILogger<UserController> _logger;
        private readonly EmailService _emailService;
        private readonly string _keyProject;
        public AdminController(ILogger<UserController> logger, meeDB _context, IConfiguration config, EmailService emailService)
        {
            _logger = logger;
            _dbContext = _context;
            _emailService = emailService;
            _hashService = new HashService(config);
            _keyProject = config["ProjectCode:AesKey"];
        }

        // อปท. ของผู้ใช้ที่ล็อกอิน อ่านจาก claim ที่ผ่านการตรวจลายเซ็นแล้ว
        // ห้ามรับจาก query string หรือ body เพราะปลอมได้ = เห็นข้อมูล อปท. อื่น
        private string CurrentOrgUnit() =>
            User.FindFirst("org_unit_code")?.Value
            ?? throw new InvalidOperationException("token ไม่มี org_unit_code กรุณาเข้าสู่ระบบใหม่");

        // ตารางลูก (measures/process/indicators) ไม่มี org_unit_code
        // จึงกันที่ตารางแม่จุดเดียว แล้ว query ลูกด้วย project_code ถึงจะปลอดภัย
        private bool OwnsProject(string projectCode) =>
            _dbContext.project.Any(o => o.code == projectCode && o.org_unit_code == CurrentOrgUnit());

        [HttpPost]
        [Route("login")]
        public async Task<IActionResult> login(RegisterUser log)
        {
            DataRespone res = new DataRespone();
            try
            {
                if (!string.IsNullOrEmpty(log.user_email) && !string.IsNullOrEmpty(log.password))
                {

                    USER result = _dbContext.user.SingleOrDefault(b => b.user_email == log.user_email);
                    if (result != null && _hashService.Verify(log.password, result.password))
                    {
                        if (result.begin_date < DateTime.Now && result.end_date > DateTime.Now)
                        {

                            res.status = "success";
                            var user = new UserModel()
                            {
                                user_email = result.user_email,
                                org_unit_code = result.org_unit_code,
                            };
                            var tokenString = _hashService.createJwtToken(user);

                            user.token = tokenString;

                            res.status = "success";
                            res.result = user;
                        }
                        else
                        {
                            res.status = "password expired";
                            res.message = "password expired";
                            res.result = result;
                        }
                        return Ok(res);
                    }
                    else
                    {
                        return StatusCode(401);
                    }

                }
                else
                {
                    return StatusCode(422);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Path} ล้มเหลว", HttpContext.Request.Path);
                return StatusCode(500);
            }
        }

        [HttpGet]
        [Route("getReport")]
        [Authorize]
        public async Task<IActionResult> getReport()
        {
            DataRespone res = new DataRespone();
            try
            {
                string orgCode = CurrentOrgUnit();
                List<GetReportModel> getReportModel = (from re in _dbContext.report
                                                       join rt in _dbContext.report_topic on re.report_code equals rt.report_code
                                                       where re.org_unit_code == orgCode
                                                       orderby re.create_date descending
                                                       select new GetReportModel()
                                                       {
                                                           persernal_type = re.persernal_type,
                                                           name_title = re.name_title,
                                                           name_title_another = re.name_title_another,
                                                           firstname = re.firstname,
                                                           lastname = re.lastname,
                                                           email = re.email,
                                                           telephone = re.telephone,
                                                           report_government_agencies = re.report_government_agencies,
                                                           report_detail = re.report_detail,
                                                           create_date = re.create_date.ToString("yyyy/MM/dd HH:mm"),
                                                           report_topic = new ReportTopic()
                                                           {
                                                               building_permit_issuance = rt.building_permit_issuance,
                                                               procurement_local_road = rt.procurement_local_road,
                                                               another = rt.another,
                                                               another_detail = rt.another_detail
                                                           },
                                                           files = _dbContext.file.Where(e => e.code_reference == re.report_code)
                                                                      .Select(o => new FileAttachment()
                                                                      {
                                                                          path = "https://meesuanruamstorage.blob.core.windows.net/meesuanruam-container/" + o.file_path,
                                                                          name = o.name,
                                                                          type = o.type,
                                                                          size = o.size
                                                                      }).ToList()
                                                       }
                                                 ).ToList();
                res.status = "success";
                res.result = getReportModel;
                return Ok(res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Path} ล้มเหลว", HttpContext.Request.Path);
                return StatusCode(500);
            }
        }

        [HttpGet]
        [Route("getComment")]
        [Authorize]
        public async Task<IActionResult> getComment()
        {
            DataRespone res = new DataRespone();
            try
            {
                string orgCode = CurrentOrgUnit();
                List<GetCommentModel> getComments = _dbContext.comment.Where(e => e.org_unit_code == orgCode).OrderByDescending(e => e.create_date).Select(o => new GetCommentModel()
                {
                    gender = o.gender,
                    occupation = o.occupation,
                    location = o.location,
                    plan_topic = o.plan_topic,
                    plan_another_detail = o.plan_another_detail,
                    detail = o.detail,
                    create_date = o.create_date.ToString("yyyy/MM/dd HH:mm"),
                    files = _dbContext.file.Where(e => e.code_reference == o.comment_code)
                                                    .Select(o => new FileAttachment()
                                                    {
                                                        path = "https://meesuanruamstorage.blob.core.windows.net/meesuanruam-container/" + o.file_path,
                                                        name = o.name,
                                                        type = o.type,
                                                        size = o.size
                                                    }).ToList()
                }).ToList();
                res.status = "success";
                res.result = getComments;
                return Ok(res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Path} ล้มเหลว", HttpContext.Request.Path);
                return StatusCode(500);
            }
        }

        [HttpPost]
        [Route("getOTP")]
        public async Task<IActionResult> getOTP(RegisterUser log)
        {
            DataRespone res = new DataRespone();
            try
            {
                if (!string.IsNullOrEmpty(log.user_email))
                {
                    USER result = _dbContext.user.SingleOrDefault(b => b.user_email == log.user_email);

                    if (result != null)
                    {
                        Random generator = new Random();
                        String otp = generator.Next(0, 1000000).ToString("D6");

                        result.token_reset_password = otp;
                        bool sendMail = await _emailService.sendMail(result.user_email, "OTP", $"<p>OTP : {otp}</p>");

                        _dbContext.SaveChanges();

                        res.status = sendMail ? "success" : "error";
                        return Ok(res);
                    }
                    else
                    {
                        return StatusCode(401);
                    }

                }
                else
                {
                    return StatusCode(422);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Path} ล้มเหลว", HttpContext.Request.Path);
                return StatusCode(500);
            }
        }

        [HttpPost]
        [Route("resetPassword")]
        public async Task<IActionResult> resetPassword(RegisterUser log)
        {
            DataRespone res = new DataRespone();
            try
            {
                if (!string.IsNullOrEmpty(log.user_email) && !string.IsNullOrEmpty(log.password) && !string.IsNullOrEmpty(log.user_otp))
                {
                    USER result = _dbContext.user.SingleOrDefault(b => b.user_email == log.user_email && b.token_reset_password == log.user_otp);
                    if (result != null)
                    {
                        string pass = _hashService.Hash(log.password);
                        result.begin_date = DateTime.Now;
                        result.end_date = DateTime.Now.AddDays(90);
                        result.password = pass;
                        result.token_reset_password = null;
                        _dbContext.SaveChanges();

                        res.status = "success";
                        res.message = "reset password success";
                    }
                    else
                    {
                        res.status = "error";
                        res.message = "can't reset password";
                    }

                    return Ok(res);
                }
                else
                {
                    return StatusCode(422);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Path} ล้มเหลว", HttpContext.Request.Path);
                return StatusCode(500);
            }
        }


        [HttpGet]
        [Route("getDashboard")]
        [Authorize]
        public async Task<IActionResult> getDashboard([FromQuery] DateRangeModel dateRange)
        {
            DataRespone res = new DataRespone();
            DashboardModel das = new DashboardModel();
            DateTime startDate = DateTime.Parse(dateRange.start);
            DateTime endDate = DateTime.Parse(dateRange.end);
            try
            {
                string orgCode = CurrentOrgUnit();
                das.building_permit_issuance = (from re in _dbContext.report
                                                join rt in _dbContext.report_topic on re.report_code equals rt.report_code
                                                where rt.building_permit_issuance == true && re.org_unit_code == orgCode
                                                && re.create_date >= startDate && re.create_date <= endDate
                                                group re by re.create_date.Date into grouped
                                                select
                                                new
                                                {
                                                    time = (long)(grouped.Key - new DateTime(1970, 1, 1)).TotalMilliseconds,
                                                    count = grouped.Count()
                                                }).Select(o => new List<long> { o.time, o.count }).ToList();

                das.procurement_local_road = (from re in _dbContext.report
                                              join rt in _dbContext.report_topic on re.report_code equals rt.report_code
                                              where rt.procurement_local_road == true && re.org_unit_code == orgCode
                                              && re.create_date >= startDate && re.create_date <= endDate
                                              group re by re.create_date.Date into grouped
                                              select
                                              new
                                              {
                                                  time = (long)(grouped.Key - new DateTime(1970, 1, 1)).TotalMilliseconds,
                                                  count = grouped.Count()
                                              }).Select(o => new List<long> { o.time, o.count }).ToList();

                das.another = (from re in _dbContext.report
                               join rt in _dbContext.report_topic on re.report_code equals rt.report_code
                               where rt.another == true && re.org_unit_code == orgCode
                               && re.create_date >= startDate && re.create_date <= endDate
                               group re by re.create_date.Date into grouped
                               select
                               new
                               {
                                   time = (long)(grouped.Key - new DateTime(1970, 1, 1)).TotalMilliseconds,
                                   count = grouped.Count()
                               }).Select(o => new List<long> { o.time, o.count }).ToList();

                das.report_per_day = _dbContext.report.Where(re => re.org_unit_code == orgCode && re.create_date >= startDate && re.create_date <= endDate).GroupBy(re => re.create_date.Date)
                    .Select(o => new List<long> { (long)(o.Key - new DateTime(1970, 1, 1)).TotalMilliseconds, o.Count() }).ToList();
                das.comment_per_day = _dbContext.comment.Where(re => re.org_unit_code == orgCode && re.create_date >= startDate && re.create_date <= endDate).GroupBy(re => re.create_date.Date)
                    .Select(o => new List<long> { (long)(o.Key - new DateTime(1970, 1, 1)).TotalMilliseconds, o.Count() }).ToList();

                res.status = "success";
                res.result = das;

                return Ok(res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Path} ล้มเหลว", HttpContext.Request.Path);
                return StatusCode(500);
            }
        }

        [HttpGet]
        [Route("getProjectList")]
        [Authorize]
        public async Task<IActionResult> getProjectList()
        {
            DataRespone res = new DataRespone();
            List<ProjectInfoResModel> resList = new List<ProjectInfoResModel>();
            try
            {
                string orgCode = CurrentOrgUnit();
                resList = _dbContext.project.Where(o => o.org_unit_code == orgCode).Select(o => new ProjectInfoResModel
                {
                    code = HashService.AesEncryptString(_keyProject, o.code),
                    name = o.name,
                    status = o.status,
                    create_date = o.create_date.ToString("yyyy/MM/dd HH:mm"),
                    create_by = o.create_by
                }).ToList();

                res.status = "success";
                res.result = resList;
                return Ok(res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Path} ล้มเหลว", HttpContext.Request.Path);
                res.status = "error";
                res.message = "ระบบขัดข้องชั่วคราว อยู่ระหว่างดำเนินการแก้ไข";
                res.result = ex.Message;
                return BadRequest(res);
            }
        }

        [HttpGet]
        [Route("getProjectInfo")]
        [Authorize]
        public async Task<IActionResult> getProjectInfo([FromQuery] string? code)
        {
            DataRespone res = new DataRespone();
            ProjectInfoResModel result = new ProjectInfoResModel();

            try
            {
                if (!string.IsNullOrEmpty(code))
                {
                    string codeDec = HashService.AesDecryptString(_keyProject, code);
                    if (!OwnsProject(codeDec)) return StatusCode(403);
                    result = _dbContext.project.Where(o => o.code == codeDec).Select(o => new ProjectInfoResModel
                    {
                        code = code,
                        name = o.name,
                        create_date = o.create_date.ToString("yyyy/MM/dd HH:mm"),
                        create_by = o.create_by,
                        status = o.status,
                        measures = _dbContext.measures.Where(p => p.project_code == o.code).Select(p => new MeasuresResModel
                        {
                            id = p.id,
                            measures_name = p.measures_name,
                            measures_checked = p.measures_checked,
                        }).ToList(),
                        process = _dbContext.process.Where(p => p.project_code == o.code).Select(p => new ProcessResModel
                        {
                            id = p.id,
                            process_name = p.process_name,
                            process_value = p.process_value,
                        }).ToList(),
                        indicators_acthievement = _dbContext.indicators_acthievement.Where(p => p.project_code == o.code).Select(p => new IndicatorsActhievementResModel
                        {
                            id = p.id,
                            acthievement_name = p.acthievement_name,
                            acthievement_value = p.acthievement_value,
                        }).ToList()
                    }).First();
                }
                else
                {
                    result = new ProjectInfoResModel
                    {
                        code = null,
                        name = null,
                        status = null,
                        create_date = null,
                        create_by = null,
                        measures = new List<MeasuresResModel>(),
                        process = new List<ProcessResModel>(),
                        indicators_acthievement = new List<IndicatorsActhievementResModel>(),
                    };
                }

                res.status = "success";
                res.result = result;
                return Ok(res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Path} ล้มเหลว", HttpContext.Request.Path);
                res.status = "error";
                res.message = "ระบบขัดข้องชั่วคราว อยู่ระหว่างดำเนินการแก้ไข";
                res.result = ex.Message;
                return BadRequest(res);
            }
        }


        [HttpPost]
        [Route("saveProjectInfo")]
        [Authorize]
        public async Task<IActionResult> saveProjectInfo(ProjectInfoReqModel body)
        {
            DataRespone res = new DataRespone();
            string projectCode = "";
            string codePrefix = "PR";
            UserModel userData = new UserModel();
            try
            {
                if (HttpContext.Request.Headers.TryGetValue("Authorization", out var authHeader) && authHeader.ToString().StartsWith("Bearer "))
                {
                    string token = authHeader.ToString().Substring("Bearer ".Length).Trim();
                    userData = _hashService.DecodingJwtToken(token);
                }

                if (!string.IsNullOrEmpty(body.code))
                {
                    projectCode = HashService.AesDecryptString(_keyProject, body.code);
                    if (!OwnsProject(projectCode)) return StatusCode(403);
                    PROJECT pro = _dbContext.project.Where(o => o.code == projectCode).First();
                    pro.name = body.name;
                    pro.status = body.status;
                }
                else
                {
                    PROJECT pro = _dbContext.project
                                 .OrderByDescending(e => Convert.ToInt32(e.code.Substring(codePrefix.Length, e.code.Length - codePrefix.Length)))
                                 .FirstOrDefault();
                    if (pro == null)
                    {
                        projectCode = $"{codePrefix}1";
                    }
                    else
                    {
                        projectCode = codePrefix + (Int64.Parse(pro.code.Substring(codePrefix.Length, pro.code.Length - codePrefix.Length)) + 1);
                    }

                    _dbContext.project.Add(new PROJECT
                    {
                        code = projectCode,
                        name = body.name,
                        create_date = DateTime.Now.AddHours(7),
                        create_by = userData.user_email,
                        status = body.status,
                        // frontend เดิมยังไม่ส่ง years มา จึงใช้ปี พ.ศ. ปัจจุบันเป็นค่าตั้งต้น
                        years = string.IsNullOrWhiteSpace(body.years)
                                ? (DateTime.Now.Year + 543).ToString()
                                : body.years,
                        org_unit_code = CurrentOrgUnit()
                    });
                }

                if (body.measures.Count > 0)
                {
                    foreach (MeasuresResModel mea in body.measures)
                    {
                        if (mea.id != 0)
                        {
                            MEASURES dbMea = _dbContext.measures.Where(o => o.project_code == projectCode && o.id == mea.id).First();
                            dbMea.measures_checked = mea.measures_checked;
                        }
                        else
                        {
                            _dbContext.measures.Add(new MEASURES
                            {
                                project_code = projectCode,
                                measures_name = mea.measures_name,
                                measures_checked = mea.measures_checked,
                            });
                        }
                    }
                }

                if (body.process.Count > 0)
                {
                    foreach (ProcessResModel pro in body.process)
                    {
                        if (pro.id != 0)
                        {
                            PROCESS dbPro = _dbContext.process.Where(o => o.project_code == projectCode && o.id == pro.id).First();
                            dbPro.process_value = pro.process_value;
                        }
                        else
                        {
                            _dbContext.process.Add(new PROCESS
                            {
                                project_code = projectCode,
                                process_name = pro.process_name,
                                process_value = pro.process_value
                            });
                        }
                    }
                }

                if (body.indicators_acthievement.Count > 0)
                {
                    foreach (IndicatorsActhievementResModel ac in body.indicators_acthievement)
                    {
                        if (ac.id != 0)
                        {
                            INDICATORS_ACTHIEVEMENT dbAc = _dbContext.indicators_acthievement.Where(o => o.project_code == projectCode && o.id == ac.id).First();
                            dbAc.acthievement_value = ac.acthievement_value;
                        }
                        else
                        {
                            _dbContext.indicators_acthievement.Add(new INDICATORS_ACTHIEVEMENT
                            {
                                project_code = projectCode,
                                acthievement_name = ac.acthievement_name,
                                acthievement_value = ac.acthievement_value
                            });
                        }
                    }
                }

                _dbContext.SaveChanges();

                res.status = "success";
                res.message = "บันทึกสำเร็จ";
                return Ok(res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Path} ล้มเหลว", HttpContext.Request.Path);
                res.status = "error";
                res.message = "ระบบขัดข้องชั่วคราว อยู่ระหว่างดำเนินการแก้ไข";
                res.result = ex.Message;
                return BadRequest(res);
            }
        }


        [HttpPost]
        [Route("deleteProjectInfo")]
        [Authorize]
        public async Task<IActionResult> deleteProjectInfo([FromQuery] string code)
        {
            DataRespone res = new DataRespone();
            try
            {
                if (!string.IsNullOrEmpty(code))
                {
                    string projectCode = HashService.AesDecryptString(_keyProject, code);
                    if (!OwnsProject(projectCode)) return StatusCode(403);
                    PROJECT pro = _dbContext.project.Where(o => o.code == projectCode).First();
                    _dbContext.project.Remove(pro);

                    List<MEASURES> mea = _dbContext.measures.Where(o => o.project_code == projectCode).ToList();
                    _dbContext.measures.RemoveRange(mea);

                    List<PROCESS> cre = _dbContext.process.Where(o => o.project_code == projectCode).ToList();
                    _dbContext.process.RemoveRange(cre);

                    List<INDICATORS_ACTHIEVEMENT> ac = _dbContext.indicators_acthievement.Where(o => o.project_code == projectCode).ToList();
                    _dbContext.indicators_acthievement.RemoveRange(ac);

                    _dbContext.SaveChanges();

                }

                res.status = "success";
                res.result = "ลบข้อมูลสำเร็จ";
                return Ok(res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Path} ล้มเหลว", HttpContext.Request.Path);
                res.status = "error";
                res.message = "ระบบขัดข้องชั่วคราว อยู่ระหว่างดำเนินการแก้ไข";
                res.result = ex.Message;
                return BadRequest(res);
            }
        }

        [HttpPost]
        [Route("exportProjectInfo")]
        [Authorize]
        public async Task<IActionResult> exportProjectInfo([FromQuery] string code)
        {
            DataRespone res = new DataRespone();
            try
            {
                string projectCode = HashService.AesDecryptString(_keyProject, code);
                if (!OwnsProject(projectCode)) return StatusCode(403);
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("คะแนนการประเมินภาพรวม");

                // เพิ่ม Header
                worksheet.Column("A").Width = 40;
                worksheet.Column("B").Width = 25;
                worksheet.Column("C").Width = 40;
                worksheet.Column("D").Width = 25;

                worksheet.Cell("A1").Value = "มาตรการ";
                worksheet.Cell("B1").Value = "คะแนนความสามารถ\r\nการดำเนินการ\r\nตามมาตรการ";

                worksheet.Cell("C1").Value = "ตัวชี้วัด";
                worksheet.Cell("D1").Value = "ระดับความสำเร็จตาม\r\nตัวชี้วัด";

                // Styling (optional but recommended)
                worksheet.Range("A1:D1").Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                worksheet.Range("A1:D1").Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
                worksheet.Range("A1:D1").Style.Font.SetBold();

                worksheet.Range("A2:A100").Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);
                worksheet.Range("A2:A100").Style.Alignment.SetVertical(XLAlignmentVerticalValues.Top);

                worksheet.Range("B2:B100").Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                worksheet.Range("B2:B100").Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
                worksheet.Range("B2:B100").Style.Font.FontSize = 72;

                worksheet.Range("C2:C100").Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);
                worksheet.Range("C2:C100").Style.Alignment.SetVertical(XLAlignmentVerticalValues.Top);

                worksheet.Range("D2:D100").Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                worksheet.Range("D2:D100").Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
                worksheet.Range("D2:D100").Style.Font.FontSize = 72;

                //header styl
                var headerRange = worksheet.Range(1, 1, 1, 4);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#91d1df");
                headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                #region มาตรการที่ 1
                worksheet.Cell(2, 1).Value = "มาตรการที่ 1 องค์กรปกครองส่วนท้องถิ่นมีการกาหนด" +
                    "\r\nนโยบาย มีการประเมินความเสี่ยง มีการวางแผนและกาหนด" +
                    "\r\nวิธีการและขั้นตอน เพื่อการป้องกันการติดสินบนในการ" +
                    "\r\nจัดซื้อจัดจ้างโครงการก่อสร้างทางหลวงท้องถิ่น";
                worksheet.Cell(2, 2).Value = _dbContext.process.Where(o => o.project_code == projectCode && o.process_name == "Policy_Process_1").Select(o => o.process_value).FirstOrDefault() ?? "0";

                worksheet.Cell(2, 3).Value = "ตัวชี้วัดที่ 1 ระดับการรับรู้ของบุคลากรในองค์กร" +
                    "\r\nปกครองส่วนท้องถิ่นและผู้มีส่วนได้ส่วนเสีย เกี่ยวกับ" +
                    "\r\nนโยบายความเสี่ยง แผนและวิธีการในการป้องกันการ" +
                    "\r\nติดสินบนในการจัดซื้อจัดจ้างโครงการก่อสร้างทาง" +
                    "\r\nหลวงท้องถิ่น";
                worksheet.Cell(2, 4).Value = _dbContext.indicators_acthievement.Where(o => o.project_code == projectCode && o.acthievement_name == "Policy_Acthievement_1").Select(o => o.acthievement_value).FirstOrDefault() ?? "0";
                #endregion

                #region มาตรการที่ 2
                worksheet.Cell(3, 1).Value = "มาตรการที่ 2 องค์กรปกครองส่วนท้องถิ่นมีการสนับสนุน" +
                    "\r\nทรัพยากรบุคคลภายในองค์กร หรือภาคประชาชน เข้ารับ" +
                    "\r\nการอบรมความรู้ ทักษะ และทัศนคติเกี่ยวกับการป้องกัน" +
                    "\r\nการติดสินบนในการจัดซื้อจัดจ้างโครงการก่อสร้างทาง" +
                    "\r\nหลวงท้องถิ่น";
                worksheet.Cell(3, 2).Value = _dbContext.process.Where(o => o.project_code == projectCode && o.process_name == "Human_Resources_Process_1").Select(o => o.process_value).FirstOrDefault() ?? "0";


                worksheet.Cell(3, 3).Value = "ตัวชี้วัดที่ 2.1 ผู้ผ่านการอบรมสามารถใช้ความรู้" +
                    "\r\nทักษะ และทัศนคติในการป้องกันและแก้ไขปัญหาการ" +
                    "\r\nติดสินบนในกระบวนการจัดซื้อจัดจ้างโครงการก่อสร้าง" +
                    "\r\nทางหลวงท้องถิ่นขององค์กรปกครองส่วนท้องถิ่นได้";
                worksheet.Cell(3, 4).Value = _dbContext.indicators_acthievement.Where(o => o.project_code == projectCode && o.acthievement_name == "Human_Resources_Acthievement_1").Select(o => o.acthievement_value).FirstOrDefault() ?? "0";
                worksheet.Cell(4, 3).Value = "ตัวชี้วัดที่ 2.2 องค์กรปกครองส่วนท้องถิ่นมีการ" +
                    "\r\nส่งบุคลากรภายในหน่วยงาน หรือภาคประชาชน" +
                    "\r\nเข้ารับการอบรมในหลักสูตรที่เกี่ยวข้องกับการป้องกัน" +
                    "\r\nและปราบปรามการทุจริตไม่น้อยกว่าปีละ 2 ครั้ง";
                worksheet.Cell(4, 4).Value = _dbContext.indicators_acthievement.Where(o => o.project_code == projectCode && o.acthievement_name == "Human_Resources_Acthievement_2").Select(o => o.acthievement_value).FirstOrDefault() ?? "0";
                worksheet.Range("A3", "A4").Merge();
                worksheet.Range("B3", "B4").Merge();
                #endregion

                #region มาตรการที่ 3
                worksheet.Cell(5, 1).Value = "มาตรการที่ 3 องค์กรปกครองส่วนท้องถิ่นมีการทวนสอบ" +
                    "\r\nการจัดทาแผนและกาหนดงบประมาณสาหรับการจัดซื้อจัด" +
                    "\r\nจ้างโครงการก่อสร้างทางหลวงท้องถิ่น";
                worksheet.Cell(5, 2).Value = _dbContext.process.Where(o => o.project_code == projectCode && o.process_name == "Budget_Road_Process_1").Select(o => o.process_value).FirstOrDefault() ?? "0";

                worksheet.Cell(5, 3).Value = "ตัวชี้วัดที่ 3 ร้อยละของแผนงานหรือโครงการก่อสร้างทาง" +
                    "\r\nหลวงท้องถิ่นที่ได้รับการทวนสอบโดยคณะกรรมการมีมติ" +
                    "\r\nเห็นชอบอย่างเป็นเอกฉันท์" +
                    "\r\nและมีการแก้ไขแผนและงบประมาณการจัดซื้อจัดจ้างในโครงการก่อสร้างทางหลวง" +
                    "\r\nท้องถิ่นตามความเห็นของคณะกรรมการ";
                worksheet.Cell(5, 4).Value = _dbContext.indicators_acthievement.Where(o => o.project_code == projectCode && o.acthievement_name == "Budget_Road_Acthievement_1").Select(o => o.acthievement_value).FirstOrDefault() ?? "0";
                #endregion

                #region มาตรการที่ 4
                worksheet.Cell(6, 1).Value = "มาตรการที่ 4 องค์กรปกครองส่วนท้องถิ่นมีการจัดทา" +
                    "\r\nและเปิดเผยเอกสารการประกวดราคา (TOR) โครงการ" +
                    "\r\nก่อสร้างทางหลวงท้องถิ่นต่อสาธารณะ";
                worksheet.Cell(6, 2).Value = _dbContext.process.Where(o => o.project_code == projectCode && o.process_name == "TOR_Process_1").Select(o => o.process_value).FirstOrDefault() ?? "0";

                worksheet.Cell(6, 3).Value = "ตัวชี้วัดที่ 4 ร้อยละของแผนงานและงบประมาณ" +
                    "\r\nโครงการก่อสร้างทางหลวงท้องถิ่นที่จัดให้มีการประชา" +
                    "\r\nพิจารณ์ มีการสรุปผลการประชาพิจารณ์ และประกาศ" +
                    "\r\nผลการประชาพิจารณ์ต่อสาธารณะอย่างเป็นทางการ";
                worksheet.Cell(6, 4).Value = _dbContext.indicators_acthievement.Where(o => o.project_code == projectCode && o.acthievement_name == "TOR_Acthievement_1").Select(o => o.acthievement_value).FirstOrDefault() ?? "0";
                #endregion

                #region มาตรการที่ 5
                worksheet.Cell(7, 1).Value = "มาตรการที่ 5 องค์กรปกครองส่วนท้องถิ่นมีการกาหนด" +
                    "\r\nและดาเนินการตามมาตรการและกลไกในการต่อต้าน" +
                    "\r\nการสมยอมการเสนอราคา ในการจัดซื้อจัดจ้าง" +
                    "\r\nโครงการก่อสร้างทางหลวงท้องถิ่นทุกรูปแบบ";
                worksheet.Cell(7, 2).Value = _dbContext.process.Where(o => o.project_code == projectCode && o.process_name == "Anti_Offer_Price_Process_1").Select(o => o.process_value).FirstOrDefault() ?? "0";

                worksheet.Cell(7, 3).Value = "ตัวชี้วัดที่ 5 ระดับความเชื่อมั่นของประชาชนต่อการ" +
                    "\r\nกระบวนการประกวดราคาและการคัดเลือกผู้รับเหมา" +
                    "\r\nในการจัดซื้อจัดจ้างโครงการก่อสร้างทางหลวงท้องถิ่น" +
                    "\r\nที่ปราศจากการสมยอมการเสนอราคา";
                worksheet.Cell(7, 4).Value = _dbContext.indicators_acthievement.Where(o => o.project_code == projectCode && o.acthievement_name == "Anti_Offer_Price_Acthievement_1").Select(o => o.acthievement_value).FirstOrDefault() ?? "0";
                #endregion

                #region มาตรการที่ 6
                worksheet.Cell(8, 1).Value = "มาตรการที่ 6 องค์กรปกครองส่วนท้องถิ่นมีการเปิดเผย" +
                    "\r\nข้อมูลสัญญาการจัดซื้อจัดจ้างโครงการก่อสร้างทางหลวง" +
                    "\r\nท้องถิ่นต่อสาธารณะเพื่อประชาพิจารณ์";
                worksheet.Cell(8, 2).Value = _dbContext.process.Where(o => o.project_code == projectCode && o.process_name == "Contact_Infomation_Process_1").Select(o => o.process_value).FirstOrDefault() ?? "0";

                worksheet.Cell(8, 3).Value = "ตัวชี้วัดที่ 6 ร้อยละของจานวนสัญญาโครงการที่มีการ" +
                    "\r\nจัดให้มีประชาพิจารณ์เพื่อตรวจสอบความถูกต้องและ" +
                    "\r\nพิจารณาถึงความสอดคล้องกับ TOR ซึ่งจะต้องมีการ" +
                    "\r\nสรุปผลการประชาพิจารณ์ และประกาศผลการประชา" +
                    "\r\nพิจารณ์ต่อสาธารณะอย่างเป็นทางการ";
                worksheet.Cell(8, 4).Value = _dbContext.indicators_acthievement.Where(o => o.project_code == projectCode && o.acthievement_name == "Contact_Infomation_Acthievement_1").Select(o => o.acthievement_value).FirstOrDefault() ?? "0";
                #endregion

                #region มาตรการที่ 7
                worksheet.Cell(9, 1).Value = "มาตรการที่ 7 องค์กรปกครองส่วนท้องถิ่นมีการให้ผู้มี" +
                    "\r\nส่วนได้ส่วนเสียเข้ามามีส่วนร่วมในการควบคุมและ" +
                    "\r\nตรวจสอบการดาเนินงานให้เป็นไปตามสัญญาการจัดซื้อจัดจ้าง";
                worksheet.Cell(9, 2).Value = _dbContext.process.Where(o => o.project_code == projectCode && o.process_name == "Examine_Process_1").Select(o => o.process_value).FirstOrDefault() ?? "0";

                worksheet.Cell(9, 3).Value = "ตัวชี้วัดที่ 7 ร้อยละของจานวนโครงการที่ให้ผู้มีส่วนได้" +
                    "\r\nส่วนเสียเข้ามามีส่วนร่วมในการควบคุมและตรวจสอบ" +
                    "\r\nการดาเนินงาน ตลอดจนมีการนาข้อเสนอแนะของผู้มี" +
                    "\r\nส่วนได้ส่วนเสียมาพิจารณาและปรับปรุงแก้ไข เพื่อให้" +
                    "\r\nเป็นไปตามข้อก าหนดในสัญญา";
                worksheet.Cell(9, 4).Value = _dbContext.indicators_acthievement.Where(o => o.project_code == projectCode && o.acthievement_name == "Examine_Acthievement_1").Select(o => o.acthievement_value).FirstOrDefault() ?? "0";
                #endregion

                #region มาตรการที่ 8
                worksheet.Cell(10, 1).Value = "มาตรการที่ 8 องค์กรปกครองส่วนท้องถิ่นมีการรายงาน" +
                    "\r\nการเบิกจ่ายในแต่ละงวดงาน เพื่อให้การดาเนินงานเป็นไป" +
                    "\r\nตามสัญญาการจัดซื้อจัดจ้าง";
                worksheet.Cell(10, 2).Value = _dbContext.process.Where(o => o.project_code == projectCode && o.process_name == "Disbursement_Report_Process_1").Select(o => o.process_value).FirstOrDefault() ?? "0";

                worksheet.Cell(10, 3).Value = "ตัวชี้วัดที่ 8 ร้อยละของจานวนสัญญาโครงการที่มีการ" +
                    "\r\nจัดให้มีการควบคุมและตรวจสอบการเบิกจ่าย ตลอดจนมี" +
                    "\r\nการรายงานการเบิกจ่ายในแต่ละงวดงานเพื่อให้การ" +
                    "\r\nด าเนินงานเป็นไปตามสัญญา";
                worksheet.Cell(10, 4).Value = _dbContext.indicators_acthievement.Where(o => o.project_code == projectCode && o.acthievement_name == "Disbursement_Report_Acthievement_1").Select(o => o.acthievement_value).FirstOrDefault() ?? "0";
                #endregion

                #region มาตรการที่ 9
                worksheet.Cell(11, 1).Value = "มาตรการที่ 9 องค์กรปกครองส่วนท้องถิ่นให้ภาค" +
                    "\r\nประชาชนที่มีคุณสมบัติเชี่ยวชาญด้านวิศวกรรมโยธาหรือ" +
                    "\r\nความเชี่ยวชาญอื่น ๆ ที่เกี่ยวข้อง เข้ามามีส่วนร่วมในการ" +
                    "\r\nตรวจรับงาน และมีกฎหมายหรือระเบียบรองรับ" +
                    "\r\nเพื่อคุ้มครองภาคประชาชนที่เข้ามามีส่วนร่วมในการตรวจรับงาน" +
                    "\r\nเพื่อให้การส่งมอบงานโครงการก่อสร้างทางหลวงท้องถิ่น" +
                    "\r\nเป็นไปตามมาตรฐานและตามรายละเอียดที่ระบุไว้ในสัญญา";
                worksheet.Cell(11, 2).Value = _dbContext.process.Where(o => o.project_code == projectCode && o.process_name == "Acceptance_Work_Process_1").Select(o => o.process_value).FirstOrDefault() ?? "0";

                worksheet.Cell(11, 3).Value = "ตัวชี้วัดที่ 9 ร้อยละของจานวนโครงการที่ดาเนินการ" +
                    "\r\nเสร็จสิ้นภายในระยะเวลาที่กาหนดไว้ในสัญญา " +
                    "\r\nและผลงานการส่งมอบเป็นไปตามมาตรฐานและตาม" +
                    "\r\nรายละเอียดที่ระบุไว้ในสัญญา";
                worksheet.Cell(11, 4).Value = _dbContext.indicators_acthievement.Where(o => o.project_code == projectCode && o.acthievement_name == "Acceptance_Work_Acthievement_1").Select(o => o.acthievement_value).FirstOrDefault() ?? "0";
                #endregion

                #region มาตรการที่ 10
                worksheet.Cell(12, 1).Value = "มาตรการที่ 10 องค์กรปกครองส่วนท้องถิ่นให้หน่วยงาน" +
                    "\r\nภาครัฐที่เกี่ยวข้องในการเฝ้าระวัง ป้องกันและปราบปราม" +
                    "\r\nการทุจริตในพื้นที่เข้ามามีส่วนร่วมในการเฝ้าสังเกตการณ์" +
                    "\r\nในการตรวจรับงาน";
                worksheet.Cell(12, 2).Value = _dbContext.process.Where(o => o.project_code == projectCode && o.process_name == "Prevention_Suppression_Process_1").Select(o => o.process_value).FirstOrDefault() ?? "0";

                worksheet.Cell(12, 3).Value = "ตัวชี้วัดที่ 10 ร้อยละของจานวนโครงการที่ดาเนินการ" +
                    "\r\nส าเร็จโดยปราศจากการร้องเรียนการทุจริต";
                worksheet.Cell(12, 4).Value = _dbContext.indicators_acthievement.Where(o => o.project_code == projectCode && o.acthievement_name == "Prevention_Suppression_Acthievement_1").Select(o => o.acthievement_value).FirstOrDefault() ?? "0";
                #endregion

                #region มาตรการที่ 11
                worksheet.Cell(13, 1).Value = "มาตรการที่ 11 องค์กรปกครองส่วนท้องถิ่นมีการ" +
                    "\r\nตรวจสอบและการประเมินผลการป้องกันการติดสินบนใน" +
                    "\r\nโครงการก่อสร้างทางหลวงท้องถิ่น และจัดทาเป็นรายงาน" +
                    "\r\nผลการด าเนินงาน";
                worksheet.Cell(13, 2).Value = _dbContext.process.Where(o => o.project_code == projectCode && o.process_name == "Prevention_Bribe_Process_1").Select(o => o.process_value).FirstOrDefault() ?? "0";

                worksheet.Cell(13, 3).Value = "ตัวชี้วัดที่ 11 องค์กรปกครองส่วนท้องถิ่นแสดงให้เห็น" +
                    "\r\nถึงรายงานผลการตรวจสอบและการประเมินผลการ" +
                    "\r\nป้องกันการติดสินบนในโครงการก่อสร้างทางหลวง" +
                    "\r\nท้องถิ่น และมีการจัดท าแผนพัฒนาปรับปรุงการ" +
                    "\r\nด าเนินการ (Improvement Plan)";
                worksheet.Cell(13, 4).Value = _dbContext.indicators_acthievement.Where(o => o.project_code == projectCode && o.acthievement_name == "Prevention_Bribe_Acthievement_1").Select(o => o.acthievement_value).FirstOrDefault() ?? "0";
                #endregion

                #region มาตรการที่ 12
                worksheet.Cell(14, 1).Value = "มาตรการที่ 12 องค์กรปกครองส่วนท้องถิ่นมีการ" +
                    "\r\nปรับปรุงการดาเนินการป้องกันการติดสินบนในโครงการ" +
                    "\r\nก่อสร้างทางหลวงท้องถิ่น";
                worksheet.Cell(14, 2).Value = _dbContext.process.Where(o => o.project_code == projectCode && o.process_name == "Amend_Prevention_Process_1").Select(o => o.process_value).FirstOrDefault() ?? "0";

                worksheet.Cell(14, 3).Value = "ตัวชี้วัดที่ 12 ระดับความสาเร็จในการดาเนินการตาม" +
                    "\r\nแผนพัฒนาการดำเนินงาน (Improvement Plan)" +
                    "\r\nในการป้องกันการติดสินบนในโครงการก่อสร้างทาง" +
                    "\r\nหลวงท้องถิ่น";
                worksheet.Cell(14, 4).Value = _dbContext.indicators_acthievement.Where(o => o.project_code == projectCode && o.acthievement_name == "Amend_Prevention_Acthievement_1").Select(o => o.acthievement_value).FirstOrDefault() ?? "0";
                #endregion

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                stream.Position = 0;
                string excelName = $"Export-{DateTime.Now:yyyyMMddHHmmss}.xlsx";

                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excelName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Path} ล้มเหลว", HttpContext.Request.Path);
                res.status = "error";
                res.message = "ระบบขัดข้องชั่วคราว อยู่ระหว่างดำเนินการแก้ไข";
                res.result = ex.Message;
                return BadRequest(res);
            }
        }


    }
}
