using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using Npgsql;

namespace HemFoundation.Services.HemStore
{
    /// <summary>
    /// Link between HEM Virtues and the separate HEM Store website (store.hemvirtues.com).
    ///
    /// HEM Virtues stays the owner of student accounts and of the points shown on the
    /// student dashboard ("Total Marks"). The store keeps its own database of what was redeemed.
    ///
    ///  - "Redeem" on the dashboard: HEM Virtues signs a short ticket (2 minutes, used once by the
    ///    store) and sends the student to the store, already logged in. No password travels.
    ///  - Store login page: the store asks HEM Virtues, server to server, whether the student's
    ///    ID and password are right. The store never stores passwords.
    ///  - Points: the store asks HEM Virtues for the student's total points.
    ///
    /// Both sides share one secret (HemStore:SharedSecret). It is never in the code or in git:
    /// set it with "dotnet user-secrets" on a developer machine, or as the environment variable
    /// HemStore__SharedSecret on the server. Without it the link is switched off.
    /// </summary>
    public class HemStoreBridge
    {
        public const string Audience = "hemstore";
        public static readonly TimeSpan TicketLife = TimeSpan.FromMinutes(2);
        public static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

        private readonly IConfiguration _config;
        private readonly string _cs;

        public HemStoreBridge(IConfiguration config)
        {
            _config = config;
            _cs = config.GetConnectionString("DefaultConnection") ?? "";
        }

        public string StoreUrl => (_config["HemStore:StoreUrl"] ?? "").TrimEnd('/');
        private string Secret => _config["HemStore:SharedSecret"] ?? "";

        /// <summary>The link works only when the store address and a long enough secret are set.</summary>
        public bool IsConfigured => StoreUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) && Secret.Length >= 32;

        // =====================================================================
        // Signing (HMAC-SHA256 with the shared secret)
        // =====================================================================
        private byte[] Sign(string text)
        {
            using var h = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
            return h.ComputeHash(Encoding.UTF8.GetBytes(text));
        }

        public static string B64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static byte[] FromB64Url(string s)
        {
            s = s.Replace('-', '+').Replace('_', '/');
            return Convert.FromBase64String(s + new string('=', (4 - s.Length % 4) % 4));
        }

        /// <summary>
        /// Ticket for the "Redeem" button: payload.signature, valid for two minutes.
        /// The store accepts each ticket id (jti) only once.
        /// </summary>
        public string CreateTicket(int studentId)
        {
            var now = DateTimeOffset.UtcNow;
            var payload = JsonSerializer.Serialize(new
            {
                sid = studentId,
                aud = Audience,
                iat = now.ToUnixTimeSeconds(),
                exp = now.Add(TicketLife).ToUnixTimeSeconds(),
                jti = Guid.NewGuid().ToString("N")
            });
            string body = B64Url(Encoding.UTF8.GetBytes(payload));
            return body + "." + B64Url(Sign(body));
        }

        /// <summary>
        /// Checks a server-to-server request from the store:
        /// signature = HMAC(secret, timestamp + "\n" + METHOD + "\n" + path + "\n" + body).
        /// </summary>
        public bool VerifyRequest(string? timestamp, string? signature, string method, string path, string body)
        {
            if (!IsConfigured || string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(signature)) return false;
            if (!long.TryParse(timestamp, out long ts)) return false;
            var age = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(ts);
            if (age.Duration() > ClockSkew) return false;

            byte[] expected = Sign(timestamp + "\n" + method.ToUpperInvariant() + "\n" + path + "\n" + body);
            byte[] given;
            try { given = FromB64Url(signature); } catch { return false; }
            return given.Length == expected.Length && CryptographicOperations.FixedTimeEquals(given, expected);
        }

        // =====================================================================
        // Student data for the store
        // =====================================================================
        public class StudentProfile
        {
            public int StudentId { get; set; }
            public string Name { get; set; } = "";
            public string? Username { get; set; }
            public string? Mobile { get; set; }
            public string? Email { get; set; }
            public string? PhotoUrl { get; set; }
            public string? Standard { get; set; }
            public string? Division { get; set; }
            public string? RollNo { get; set; }
            public string? School { get; set; }
            public string? Address { get; set; }
            public string? City { get; set; }
            public string? State { get; set; }
            public string? Pincode { get; set; }
            public bool IsActive { get; set; }
            /// <summary>The same number as "Total Marks" on the student dashboard.</summary>
            public int TotalPoints { get; set; }
        }

        public async Task<StudentProfile?> StudentAsync(int studentId)
        {
            await using var c = new NpgsqlConnection(_cs);
            var p = await c.QueryFirstOrDefaultAsync<StudentProfile>(@"
                SELECT u.userid AS StudentId,
                       btrim(coalesce(u.firstname, '') || ' ' || coalesce(u.lastname, '')) AS Name,
                       an.username AS Username, u.mobileno AS Mobile, u.emailid AS Email,
                       nullif(nullif(u.photourl, '-'), '') AS PhotoUrl,
                       btrim(s.standard) AS Standard, s.division AS Division, s.rollno AS RollNo,
                       (SELECT i.""Name"" FROM wj_studentassociates a JOIN wj_institutes i ON i.instituteid = a.instituteid
                        WHERE a.studentid = s.studentid ORDER BY (a.isprimaryassociate::text IN ('1', 'true')) DESC LIMIT 1) AS School,
                       u.addressline1 AS Address, u.cityname AS City, u.addressline3 AS State, u.pincode AS Pincode,
                       coalesce(u.isactive::text IN ('1', 'true'), false) AS IsActive
                FROM users u
                JOIN wj_students s ON s.studentid = u.userid
                LEFT JOIN aspnetusers an ON an.id = u.aspnetuserid
                WHERE u.userid = @studentId", new { studentId });
            if (p == null) return null;

            // Same source as the dashboard card (LearnController → hf_sp_getstudentmaincourses).
            p.TotalPoints = await c.ExecuteScalarAsync<int?>(
                "SELECT globaltotalmarks FROM public.hf_sp_getstudentmaincourses(@studentId) LIMIT 1", new { studentId }) ?? 0;
            return p;
        }
    }
}
