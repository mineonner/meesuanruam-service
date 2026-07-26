using System.Text.RegularExpressions;

namespace meesuanruam_service.services
{
    /// <summary>
    /// เก็บไฟล์แนบลงดิสก์ใน volume แทน Azure Blob เดิม
    ///
    /// path ทุกเส้นถูกประกอบโดยเซิร์ฟเวอร์เท่านั้น ห้ามรับจาก client
    /// ของเดิมเอา pathFile จาก client มาต่อชื่อไฟล์ตรงๆ ซึ่งบน blob แค่ทำให้ key เพี้ยน
    /// แต่บน filesystem จริงคือช่องเขียนทับไฟล์อะไรก็ได้ในเครื่อง
    /// </summary>
    public class FileStorageService
    {
        public const long MaxBytesPerFile = 10 * 1024 * 1024;   // ตรงกับที่ frontend จำกัดไว้
        public const int MaxFilesPerRecord = 10;

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".webp",
            ".doc", ".docx", ".xls", ".xlsx", ".txt", ".csv"
        };

        // ยอมเฉพาะรูปแบบที่ saveReport/saveComment สร้างขึ้นเท่านั้น
        private static readonly Regex PathFilePattern = new(@"^(report/R|comment/C)[0-9]+$", RegexOptions.Compiled);

        // คีย์ตัวชี้วัดที่ไฟล์ของแบบประเมินผูกอยู่ เช่น Policy_Process_1 / TOR_Acthievement_1
        private static readonly Regex MeasuresPrefixPattern = new(@"^[A-Za-z_]+_(Process|Acthievement)_[0-9]+$", RegexOptions.Compiled);

        private static readonly Regex ProjectCodePattern = new(@"^PR[0-9]+$", RegexOptions.Compiled);

        private readonly string _root;

        public FileStorageService(IConfiguration config)
        {
            _root = Path.GetFullPath(config["Uploads:Root"] ?? "/app/uploads");
        }

        public static bool IsAllowedExtension(string fileName) =>
            AllowedExtensions.Contains(Path.GetExtension(fileName));

        public static string AllowedExtensionList => string.Join(", ", AllowedExtensions);

        /// <summary>แยก "comment/C6" เป็น folder + code พร้อมตรวจรูปแบบ</summary>
        public static bool TryParsePathFile(string? pathFile, out string folder, out string code)
        {
            folder = code = string.Empty;
            if (string.IsNullOrWhiteSpace(pathFile) || !PathFilePattern.IsMatch(pathFile.Trim()))
            {
                return false;
            }

            string[] parts = pathFile.Trim().Split('/');
            folder = parts[0];
            code = parts[1];
            return true;
        }

        /// <summary>
        /// ตัดทุกอย่างที่เป็น path ออกจากชื่อไฟล์ เหลือแค่ชื่อล้วน
        /// คืน null ถ้าเหลือแล้วใช้ไม่ได้ เช่น ".." หรือชื่อว่าง
        /// </summary>
        public static string? SanitizeFileName(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            // GetFileName ตัด directory ทิ้งให้ แต่ยังต้องกันอักขระต้องห้ามของ filesystem เอง
            string name = Path.GetFileName(fileName.Trim());
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }

            name = name.Trim('.', ' ');
            return string.IsNullOrWhiteSpace(name) || name.Length > 150 ? null : name;
        }

        public static string BuildRelativePath(string orgUnitCode, string folder, string code, string safeFileName) =>
            $"{orgUnitCode}/{folder}/{code}/{safeFileName}";

        public static bool IsValidMeasuresPrefix(string? prefix) =>
            !string.IsNullOrWhiteSpace(prefix) && MeasuresPrefixPattern.IsMatch(prefix.Trim());

        public static bool IsValidProjectCode(string? code) =>
            !string.IsNullOrWhiteSpace(code) && ProjectCodePattern.IsMatch(code.Trim());

        /// <summary>
        /// ของเดิมเก็บที่ project/{code}/{ชื่อไฟล์} ไม่มีชั้นของตัวชี้วัด
        /// ทำให้ไฟล์ชื่อซ้ำกันคนละตัวชี้วัดทับกันเอง จึงเพิ่มชั้น prefix เข้ามา
        /// </summary>
        public static string BuildProjectRelativePath(string orgUnitCode, string projectCode, string measuresPrefix, string safeFileName) =>
            $"{orgUnitCode}/project/{projectCode}/{measuresPrefix}/{safeFileName}";

        public void Delete(string relativePath)
        {
            if (TryGetFullPath(relativePath, out string fullPath))
            {
                File.Delete(fullPath);
            }
        }

        public async Task SaveAsync(string relativePath, Stream content)
        {
            string fullPath = ResolveOrThrow(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            using FileStream target = File.Create(fullPath);
            await content.CopyToAsync(target);
        }

        public bool TryGetFullPath(string relativePath, out string fullPath)
        {
            fullPath = string.Empty;
            try
            {
                string resolved = ResolveOrThrow(relativePath);
                if (!File.Exists(resolved))
                {
                    return false;
                }

                fullPath = resolved;
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        /// <summary>
        /// ด่านสุดท้าย: path ที่คลี่ออกมาแล้วต้องอยู่ใต้ _root เสมอ
        /// ต่อให้ชั้นบนหลุดมา ตรงนี้ยังกันการเขียน/อ่านนอกโฟลเดอร์ได้
        /// </summary>
        private string ResolveOrThrow(string relativePath)
        {
            string combined = Path.GetFullPath(Path.Combine(_root, relativePath));
            string rootWithSeparator = _root.EndsWith(Path.DirectorySeparatorChar)
                ? _root
                : _root + Path.DirectorySeparatorChar;

            if (!combined.StartsWith(rootWithSeparator, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"path '{relativePath}' หลุดออกนอกโฟลเดอร์ที่อนุญาต");
            }

            return combined;
        }
    }
}
