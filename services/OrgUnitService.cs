using meesuanruam_service.DTO;

namespace meesuanruam_service.services
{
    /// <summary>
    /// หา อปท. (org_unit_code) ของ request ที่ยิงเข้ามา
    /// ใช้กับ endpoint สาธารณะที่ผู้ใช้ไม่ได้ล็อกอิน จึงมีแค่โดเมนเป็นสัญญาณ
    /// ฝั่ง admin ห้ามใช้ตัวนี้ ให้อ่านจาก claim ใน JWT แทน เพราะ Origin ปลอมได้
    /// </summary>
    public class OrgUnitService
    {
        private readonly meeDB _dbContext;

        public OrgUnitService(meeDB dbContext)
        {
            _dbContext = dbContext;
        }

        public string ResolveFromOrigin(string? origin)
        {
            if (string.IsNullOrWhiteSpace(origin))
            {
                throw new InvalidOperationException("request ไม่มี Origin header จึงระบุ อปท. ไม่ได้");
            }

            string wanted = Normalize(origin);

            // ponytail: ORG_UNIT มี 6 แถว ดึงมาเทียบใน memory ง่ายกว่าเขียน SQL ให้ normalize
            ORG_UNITMatch? match = _dbContext.org_unit
                .Select(o => new ORG_UNITMatch { code = o.code, domain_name = o.domain_name })
                .AsEnumerable()
                .FirstOrDefault(o => Normalize(o.domain_name) == wanted);

            if (match == null)
            {
                // ห้าม fallback เป็น '0001' เงียบๆ ไม่งั้นข้อมูลของทุก อปท. จะปนกันโดยไม่มีใครรู้
                throw new InvalidOperationException($"โดเมน '{origin}' ไม่ตรงกับ อปท. ใดในตาราง ORG_UNIT");
            }

            return match.code;
        }

        private static string Normalize(string url) => url.Trim().TrimEnd('/').ToLowerInvariant();

        private class ORG_UNITMatch
        {
            public string code { get; set; }
            public string domain_name { get; set; }
        }
    }
}
