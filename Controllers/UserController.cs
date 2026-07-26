using Microsoft.AspNetCore.Mvc;
using meesuanruam_service.DTO;
using meesuanruam_service.DTO.table;
using meesuanruam_service.model.request;
using meesuanruam_service.model.respone;
using meesuanruam_service.services;

namespace meesuanruam_service.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/meesuanruam")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly meeDB _dbContext;
        private readonly ILogger<UserController> _logger;
        private readonly OrgUnitService _orgUnitService;
        public UserController(ILogger<UserController> logger, meeDB _context, OrgUnitService orgUnitService)
        {
            _logger = logger;
            _dbContext = _context;
            _orgUnitService = orgUnitService;
        }

        // อปท. เจ้าของข้อมูล มาจากโดเมนที่ยิงเข้ามา ผู้ร้องไม่ได้ล็อกอินจึงไม่มี JWT ให้อ่าน
        private string CurrentOrgUnit() =>
            _orgUnitService.ResolveFromOrigin(HttpContext.Request.Headers["Origin"].ToString());

        [HttpGet]
        [Route("getReport")]
        public async Task<IActionResult> getReport()
        {
            DataRespone res = new DataRespone();
            string folder = "report";
            try
            {
                res.status = "success";
                res.message = "success";

                return Ok(res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Path} ล้มเหลว", HttpContext.Request.Path);
                res.status = "error";
                res.message = ex.Message;
                return BadRequest(res);
            }
        }


        [HttpPost]
        [Route("saveReport")]
        public async Task<IActionResult> saveReport(SaveReport re)
        {
            DataRespone res = new DataRespone();
            string folder = "report";
            try
            {
                string orgUnitCode = CurrentOrgUnit();
                string code;
                List<REPORT> reportTB = _dbContext.report
                            .OrderByDescending(e => Convert.ToInt32(e.report_code.Substring(1, e.report_code.Length - 1)))
                            .Take(1)
                            .ToList();

                if (reportTB.Count == 0)
                {
                    code = "R1";
                }
                else
                {
                    code = "R" + (Int64.Parse(reportTB[0].report_code.Substring(1, reportTB[0].report_code.Length - 1)) + 1);
                }

                _dbContext.report.Add(new REPORT()
                {
                    report_code = code,
                    persernal_type = re.persernal_type,
                    name_title = re.name_title,
                    name_title_another = re.name_title_another,
                    firstname = re.firstname,
                    lastname = re.lastname,
                    email = re.email,
                    telephone = re.telephone,
                    report_government_agencies = re.report_government_agencies,
                    report_detail = re.report_detail,
                    create_date = DateTime.Now,
                    org_unit_code = orgUnitCode
                });

                _dbContext.report_topic.Add(new REPORT_TOPIC()
                {
                    report_code = code,
                    building_permit_issuance = re.report_topic.building_permit_issuance,
                    procurement_local_road = re.report_topic.procurement_local_road,
                    another = re.report_topic.another,
                    another_detail = re.report_topic.another_detail,
                });

                // แถว FILE ถูกเขียนที่ UploadController หลังไฟล์ลงดิสก์สำเร็จแล้ว
                // ถ้าเขียนตรงนี้ (แบบเดิม) แล้วอัปโหลดพลาด จะเหลือแถวชี้ไฟล์ที่ไม่มีอยู่
                _dbContext.SaveChanges();


                res.status = "success";
                res.message = folder + "/" + code;

                return Ok(res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Path} ล้มเหลว", HttpContext.Request.Path);
                return StatusCode(500);
            }



        }


        [HttpPost]
        [Route("saveComment")]
        public async Task<IActionResult> saveComment(SaveComment saveCom)
        {
            DataRespone res = new DataRespone();
            string folder = "comment";
            try
            {
                string orgUnitCode = CurrentOrgUnit();
                string code;
                List<COMMENT> comTB = _dbContext.comment
                            .OrderByDescending(e => Convert.ToInt32(e.comment_code.Substring(1, e.comment_code.Length - 1)))
                            .Take(1)
                            .ToList();

                if (comTB.Count == 0)
                {
                    code = "C1";
                }
                else
                {
                    code = "C" + (Int64.Parse(comTB[0].comment_code.Substring(1, comTB[0].comment_code.Length - 1)) + 1);
                }

                _dbContext.comment.Add(new COMMENT()
                {
                    comment_code = code,
                    gender = saveCom.gender,
                    occupation = saveCom.occupation,
                    location = saveCom.location,
                    plan_topic = saveCom.plan_topic,
                    plan_another_detail = saveCom.plan_another_detail,
                    detail = saveCom.detail,
                    create_date = DateTime.Now,
                    org_unit_code = orgUnitCode
                });

                // แถว FILE ถูกเขียนที่ UploadController หลังไฟล์ลงดิสก์สำเร็จแล้ว
                // ถ้าเขียนตรงนี้ (แบบเดิม) แล้วอัปโหลดพลาด จะเหลือแถวชี้ไฟล์ที่ไม่มีอยู่
                _dbContext.SaveChanges();


                res.status = "success";
                res.message = folder + "/" + code;

                return Ok(res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Path} ล้มเหลว", HttpContext.Request.Path);
                return StatusCode(500);
            }
        }
    }
}
