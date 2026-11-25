using ClosedXML.Excel;
using Dapper;
using Npgsql;
using System.Globalization;
using System.Text.RegularExpressions;

namespace HemFoundation.Services.Academic
{
    /// <summary>
    /// Academic module (subjects, terms, exam rules, marks, report card) on PostgreSQL.
    /// Replaces the old SQL Server procedures HF_SP_SaveInstituteSubject, HF_SP_SaveExamConfiguration,
    /// HF_SP_UpsertStudentMark, HF_SP_GetStudentHolisticAcademicReport and SP_GetStudentActivityLogMarks
    /// with the same rules. Every method is limited to one school (instituteId = the school's user id).
    /// Tables: Data/Scripts/RunOnce_Academic_2026_11_26.sql
    /// </summary>
    public class AcademicService
    {
        private readonly string _connectionString;
        private readonly ILogger<AcademicService> _logger;

        public AcademicService(IConfiguration configuration, ILogger<AcademicService> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? "";
            _logger = logger;
        }

        private NpgsqlConnection Open() => new(_connectionString);

        public class AcademicException : Exception { public AcademicException(string m) : base(m) { } }

        // =====================================================================
        // Records returned to the page
        // =====================================================================
        public record Subject(int SubjectId, string SubjectName, string? SubjectCode, int ExamCount);
        public record Term(int TermId, string TermName, string AcademicYear, int ExamCount);
        public record Exam(int ConfigId, int TermId, string TermName, string AcademicYear, string Standard, int SubjectId, string SubjectName,
            decimal MaxMarks, decimal PassingMarks, int Students, int Entered, int Absent, int Passed, decimal? Average);
        public record StudentRow(int StudentId, string Name, string? RollNo, string? Division, string Standard);
        public record MarkRow(int StudentId, string Name, string? RollNo, string? Division, decimal? MarksObtained, bool IsAbsent, string? Remarks, bool HasMark);
        public class MarkInput { public int StudentId { get; set; } public decimal? MarksObtained { get; set; } public bool IsAbsent { get; set; } public string? Remarks { get; set; } }
        public record SaveResult(bool Success, int Saved, List<string> Errors);

        // =====================================================================
        // Is the table set up? (so the page can explain instead of failing)
        // =====================================================================
        public async Task<bool> TablesExistAsync()
        {
            try
            {
                await using var c = Open();
                return await c.ExecuteScalarAsync<bool>(
                    "SELECT to_regclass('public.hf_tbl_studentmarks') IS NOT NULL AND to_regclass('public.hf_tbl_examconfigurations') IS NOT NULL");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Academic: could not check tables");
                return false;
            }
        }

        // =====================================================================
        // Students of a school
        // =====================================================================
        private const string StudentsSql = @"
            SELECT DISTINCT ON (s.studentid)
                   s.studentid AS StudentId,
                   btrim(coalesce(u.firstname, '') || ' ' || coalesce(u.lastname, '')) AS Name,
                   s.rollno AS RollNo, s.division AS Division, s.standard AS Standard
            FROM wj_studentassociates a
            JOIN wj_students s ON s.studentid = a.studentid
            LEFT JOIN users u ON u.userid = s.studentid
            WHERE a.instituteid = @inst";

        public async Task<List<string>> StandardsAsync(int inst)
        {
            await using var c = Open();
            var list = (await c.QueryAsync<string>(@"
                SELECT DISTINCT btrim(s.standard) FROM wj_studentassociates a
                JOIN wj_students s ON s.studentid = a.studentid
                WHERE a.instituteid = @inst AND coalesce(btrim(s.standard), '') <> ''", new { inst })).ToList();
            return list.OrderBy(StandardOrder).ThenBy(x => x).ToList();
        }

        /// <summary>Sorts "3", "Class 4", "Std VII", "10th" by their class number.</summary>
        public static int StandardOrder(string s)
        {
            var m = Regex.Match(s ?? "", @"\d+");
            if (m.Success) return int.Parse(m.Value);
            string[] roman = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII" };
            var token = Regex.Match((s ?? "").ToUpperInvariant(), @"\b(XII|XI|X|IX|VIII|VII|VI|V|IV|III|II|I)\b");
            return token.Success ? Array.IndexOf(roman, token.Value) + 1 : 99;
        }

        public async Task<List<StudentRow>> StudentsAsync(int inst, string? standard)
        {
            await using var c = Open();
            var rows = await c.QueryAsync<StudentRow>(StudentsSql + (string.IsNullOrEmpty(standard) ? "" : " AND btrim(s.standard) = @standard") +
                " ORDER BY s.studentid", new { inst, standard });
            return SortStudents(rows).ToList();
        }

        private static IEnumerable<T> SortStudents<T>(IEnumerable<T> rows) where T : notnull
        {
            static (int, string, string) Key(object r)
            {
                string? roll = r switch { StudentRow s => s.RollNo, MarkRow m => m.RollNo, _ => null };
                string name = r switch { StudentRow s => s.Name, MarkRow m => m.Name, _ => "" };
                int n = int.TryParse(roll, out var v) ? v : int.MaxValue;
                return (n, roll ?? "", name);
            }
            return rows.OrderBy(r => Key(r).Item1).ThenBy(r => Key(r).Item2).ThenBy(r => Key(r).Item3);
        }

        public async Task<bool> StudentBelongsAsync(int inst, int studentId)
        {
            await using var c = Open();
            return await c.ExecuteScalarAsync<bool>(
                "SELECT EXISTS (SELECT 1 FROM wj_studentassociates WHERE instituteid = @inst AND studentid = @studentId)", new { inst, studentId });
        }

        // =====================================================================
        // Subjects
        // =====================================================================
        public async Task<List<Subject>> SubjectsAsync(int inst)
        {
            await using var c = Open();
            return (await c.QueryAsync<Subject>(@"
                SELECT s.subjectid AS SubjectId, coalesce(s.subjectname, '') AS SubjectName, s.subjectcode AS SubjectCode,
                       (SELECT count(*) FROM hf_tbl_examconfigurations e WHERE e.subjectid = s.subjectid)::int AS ExamCount
                FROM hf_tbl_institutesubjects s
                WHERE s.instituteid = @inst AND coalesce(s.isactive, B'1') = B'1'
                ORDER BY lower(coalesce(s.subjectname, ''))", new { inst })).ToList();
        }

        public async Task<int> SaveSubjectAsync(int inst, int? subjectId, string name, string? code, int userId)
        {
            name = (name ?? "").Trim(); code = string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
            if (name.Length < 2 || name.Length > 100) throw new AcademicException("Enter the subject name (2 to 100 characters).");
            if (code != null && code.Length > 20) throw new AcademicException("The subject code must be 20 characters or fewer.");

            await using var c = Open();
            bool duplicate = await c.ExecuteScalarAsync<bool>(@"
                SELECT EXISTS (SELECT 1 FROM hf_tbl_institutesubjects WHERE instituteid = @inst AND coalesce(isactive, B'1') = B'1'
                               AND lower(subjectname) = lower(@name) AND subjectid <> coalesce(@subjectId, 0))", new { inst, name, subjectId });
            if (duplicate) throw new AcademicException($"“{name}” is already in your subject list.");

            if (subjectId is > 0)
            {
                int n = await c.ExecuteAsync(@"UPDATE hf_tbl_institutesubjects SET subjectname = @name, subjectcode = @code
                                               WHERE subjectid = @subjectId AND instituteid = @inst AND coalesce(isactive, B'1') = B'1'", new { inst, subjectId, name, code });
                if (n == 0) throw new AcademicException("Subject not found.");
                return subjectId.Value;
            }
            return await c.ExecuteScalarAsync<int>(@"INSERT INTO hf_tbl_institutesubjects (instituteid, subjectname, subjectcode, isactive)
                                                     VALUES (@inst, @name, @code, B'1') RETURNING subjectid", new { inst, name, code });
        }

        /// <summary>Hidden from new exams; exams already set up keep it (the history stays correct).</summary>
        public async Task RemoveSubjectAsync(int inst, int subjectId)
        {
            await using var c = Open();
            int n = await c.ExecuteAsync("UPDATE hf_tbl_institutesubjects SET isactive = B'0' WHERE subjectid = @subjectId AND instituteid = @inst",
                new { inst, subjectId });
            if (n == 0) throw new AcademicException("Subject not found.");
        }

        // =====================================================================
        // Terms
        // =====================================================================
        public async Task<List<Term>> TermsAsync(int inst)
        {
            await using var c = Open();
            return (await c.QueryAsync<Term>(@"
                SELECT t.termid AS TermId, coalesce(t.termname, '') AS TermName, coalesce(t.academicyear, '') AS AcademicYear,
                       (SELECT count(*) FROM hf_tbl_examconfigurations e WHERE e.termid = t.termid)::int AS ExamCount
                FROM hf_tbl_academicterms t
                WHERE t.instituteid = @inst AND coalesce(t.isactive, B'1') = B'1'
                ORDER BY t.academicyear DESC, t.termid", new { inst })).ToList();
        }

        public static string CurrentAcademicYear(DateTime today)
        {
            int start = today.Month >= 4 ? today.Year : today.Year - 1;   // April to March
            return $"{start}-{(start + 1) % 100:00}";
        }

        public async Task<int> SaveTermAsync(int inst, int? termId, string name, string year, int userId)
        {
            name = (name ?? "").Trim(); year = (year ?? "").Trim();
            if (name.Length < 2 || name.Length > 100) throw new AcademicException("Enter the term name, e.g. Half Yearly.");
            if (!Regex.IsMatch(year, @"^\d{4}-\d{2}(\d{2})?$")) throw new AcademicException("Enter the academic year like 2026-27.");

            await using var c = Open();
            bool duplicate = await c.ExecuteScalarAsync<bool>(@"
                SELECT EXISTS (SELECT 1 FROM hf_tbl_academicterms WHERE instituteid = @inst AND coalesce(isactive, B'1') = B'1'
                               AND lower(termname) = lower(@name) AND academicyear = @year AND termid <> coalesce(@termId, 0))", new { inst, name, year, termId });
            if (duplicate) throw new AcademicException($"“{name} ({year})” already exists.");

            if (termId is > 0)
            {
                int n = await c.ExecuteAsync("UPDATE hf_tbl_academicterms SET termname = @name, academicyear = @year WHERE termid = @termId AND instituteid = @inst AND coalesce(isactive, B'1') = B'1'",
                    new { inst, termId, name, year });
                if (n == 0) throw new AcademicException("Term not found.");
                return termId.Value;
            }
            return await c.ExecuteScalarAsync<int>(@"INSERT INTO hf_tbl_academicterms (instituteid, termname, academicyear, isactive)
                                                     VALUES (@inst, @name, @year, B'1') RETURNING termid", new { inst, name, year });
        }

        public async Task RemoveTermAsync(int inst, int termId)
        {
            await using var c = Open();
            bool used = await c.ExecuteScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM hf_tbl_examconfigurations WHERE termid = @termId AND instituteid = @inst)", new { inst, termId });
            if (used) throw new AcademicException("This term has exams set up. Remove those exams first.");
            int n = await c.ExecuteAsync("UPDATE hf_tbl_academicterms SET isactive = B'0' WHERE termid = @termId AND instituteid = @inst", new { inst, termId });
            if (n == 0) throw new AcademicException("Term not found.");
        }

        // =====================================================================
        // Exams (exam configurations)
        // =====================================================================
        private const string ExamsSql = @"
            WITH roster AS (
                SELECT btrim(s.standard) AS standard, count(DISTINCT s.studentid)::int AS students
                FROM wj_studentassociates a JOIN wj_students s ON s.studentid = a.studentid
                WHERE a.instituteid = @inst GROUP BY btrim(s.standard)
            ),
            marks AS (
                SELECT DISTINCT ON (configid, studentid) markid, configid, instituteid,
                       coalesce(marksobtained, 0) AS marksobtained, coalesce(isabsent = B'1', false) AS isabsent
                FROM hf_tbl_studentmarks WHERE instituteid = @inst
                ORDER BY configid, studentid, markid DESC
            )
            SELECT e.configid AS ConfigId, e.termid AS TermId, coalesce(t.termname, '') AS TermName, coalesce(t.academicyear, '') AS AcademicYear,
                   coalesce(btrim(e.standard), '') AS Standard, e.subjectid AS SubjectId, coalesce(sub.subjectname, '') AS SubjectName,
                   coalesce(e.maxmarks, 0) AS MaxMarks, coalesce(e.passingmarks, 0) AS PassingMarks,
                   coalesce(r.students, 0) AS Students,
                   count(m.markid)::int AS Entered,
                   count(m.markid) FILTER (WHERE m.isabsent)::int AS Absent,
                   count(m.markid) FILTER (WHERE NOT m.isabsent AND m.marksobtained >= e.passingmarks)::int AS Passed,
                   round(avg(m.marksobtained) FILTER (WHERE NOT m.isabsent), 2) AS Average
            FROM hf_tbl_examconfigurations e
            JOIN hf_tbl_academicterms t ON t.termid = e.termid
            JOIN hf_tbl_institutesubjects sub ON sub.subjectid = e.subjectid
            LEFT JOIN roster r ON r.standard = btrim(e.standard)
            LEFT JOIN marks m ON m.configid = e.configid
            WHERE e.instituteid = @inst";

        private const string ExamsGroupBy = @"
            GROUP BY e.configid, e.termid, e.standard, e.subjectid, e.maxmarks, e.passingmarks, t.termname, t.academicyear, sub.subjectname, r.students";

        public async Task<List<Exam>> ExamsAsync(int inst)
        {
            await using var c = Open();
            var list = (await c.QueryAsync<Exam>(ExamsSql + ExamsGroupBy, new { inst })).ToList();
            return list.OrderByDescending(e => e.AcademicYear).ThenBy(e => e.TermId).ThenBy(e => StandardOrder(e.Standard)).ThenBy(e => e.Standard).ThenBy(e => e.SubjectName).ToList();
        }

        public async Task<Exam?> ExamAsync(int inst, int configId)
        {
            await using var c = Open();
            return await c.QueryFirstOrDefaultAsync<Exam>(ExamsSql + " AND e.configid = @configId" + ExamsGroupBy, new { inst, configId });
        }

        /// <summary>Same rule as HF_SP_SaveExamConfiguration: one rule per term + standard + subject; saving again updates the marks.</summary>
        public async Task<(int ConfigId, bool Updated)> SaveExamAsync(int inst, int termId, string standard, int subjectId, decimal maxMarks, decimal passingMarks)
        {
            standard = (standard ?? "").Trim();
            if (string.IsNullOrEmpty(standard)) throw new AcademicException("Choose the standard.");
            if (maxMarks <= 0 || maxMarks > 1000) throw new AcademicException("Max marks must be between 1 and 1000.");
            if (passingMarks < 0 || passingMarks > maxMarks) throw new AcademicException("Passing marks must be between 0 and the max marks.");

            await using var c = Open();
            bool termOk = await c.ExecuteScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM hf_tbl_academicterms WHERE termid = @termId AND instituteid = @inst AND coalesce(isactive, B'1') = B'1')", new { inst, termId });
            if (!termOk) throw new AcademicException("Choose a term.");
            bool subjectOk = await c.ExecuteScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM hf_tbl_institutesubjects WHERE subjectid = @subjectId AND instituteid = @inst AND coalesce(isactive, B'1') = B'1')", new { inst, subjectId });
            if (!subjectOk) throw new AcademicException("Choose a subject.");

            // Lowering max marks below marks already entered would make those marks invalid.
            decimal? highest = await c.ExecuteScalarAsync<decimal?>(@"
                SELECT max(m.marksobtained) FROM hf_tbl_studentmarks m JOIN hf_tbl_examconfigurations e ON e.configid = m.configid
                WHERE e.instituteid = @inst AND e.termid = @termId AND btrim(e.standard) = @standard AND e.subjectid = @subjectId AND m.isabsent IS DISTINCT FROM B'1'",
                new { inst, termId, standard, subjectId });
            if (highest is decimal h && h > maxMarks) throw new AcademicException($"A student already has {h:0.##} marks in this exam. Max marks cannot be lower than that.");

            await c.OpenAsync();
            await using var tx = await c.BeginTransactionAsync();
            // One rule per school + term + standard + subject (the old MERGE key); the lock stops two saves adding it twice.
            await c.ExecuteAsync("SELECT pg_advisory_xact_lock(881001, @inst)", new { inst }, tx);
            int? existing = await c.ExecuteScalarAsync<int?>(@"
                SELECT configid FROM hf_tbl_examconfigurations
                WHERE instituteid = @inst AND termid = @termId AND btrim(standard) = @standard AND subjectid = @subjectId
                ORDER BY configid LIMIT 1", new { inst, termId, standard, subjectId }, tx);
            int configId;
            if (existing is int id)
            {
                await c.ExecuteAsync("UPDATE hf_tbl_examconfigurations SET maxmarks = @maxMarks, passingmarks = @passingMarks WHERE configid = @id",
                    new { id, maxMarks, passingMarks }, tx);
                configId = id;
            }
            else
            {
                configId = await c.ExecuteScalarAsync<int>(@"
                    INSERT INTO hf_tbl_examconfigurations (instituteid, termid, standard, subjectid, maxmarks, passingmarks)
                    VALUES (@inst, @termId, @standard, @subjectId, @maxMarks, @passingMarks) RETURNING configid",
                    new { inst, termId, standard, subjectId, maxMarks, passingMarks }, tx);
            }
            await tx.CommitAsync();
            return (configId, existing != null);
        }

        public async Task RemoveExamAsync(int inst, int configId)
        {
            await using var c = Open();
            bool hasMarks = await c.ExecuteScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM hf_tbl_studentmarks WHERE configid = @configId AND instituteid = @inst)", new { inst, configId });
            if (hasMarks) throw new AcademicException("Marks are already entered for this exam, so it cannot be removed.");
            int n = await c.ExecuteAsync("DELETE FROM hf_tbl_examconfigurations WHERE configid = @configId AND instituteid = @inst", new { inst, configId });
            if (n == 0) throw new AcademicException("Exam not found.");
        }

        // =====================================================================
        // Marks
        // =====================================================================
        public async Task<List<MarkRow>> MarksAsync(int inst, int configId)
        {
            var exam = await ExamAsync(inst, configId) ?? throw new AcademicException("Exam not found.");
            await using var c = Open();
            var rows = await c.QueryAsync<MarkRow>(@"
                SELECT st.StudentId, st.Name, st.RollNo, st.Division,
                       m.marksobtained AS MarksObtained, coalesce(m.isabsent = B'1', false) AS IsAbsent, m.remarks AS Remarks, (m.markid IS NOT NULL) AS HasMark
                FROM (" + StudentsSql + @" AND btrim(s.standard) = @standard ORDER BY s.studentid) st
                LEFT JOIN LATERAL (SELECT x.markid, x.marksobtained, x.isabsent, x.remarks FROM hf_tbl_studentmarks x
                                   WHERE x.studentid = st.StudentId AND x.configid = @configId AND x.instituteid = @inst
                                   ORDER BY x.markid DESC LIMIT 1) m ON true",
                new { inst, configId, standard = exam.Standard.Trim() });
            return SortStudents(rows).ToList();
        }

        /// <summary>
        /// Same checks as HF_SP_UpsertStudentMark (student of this school, marks 0 to max) for every row;
        /// nothing is saved unless every row is valid, as with the old transaction.
        /// </summary>
        public async Task<SaveResult> SaveMarksAsync(int inst, int configId, IList<(string Label, MarkInput Mark)> rows, int userId, List<string>? earlierErrors = null)
        {
            var exam = await ExamAsync(inst, configId) ?? throw new AcademicException("Exam not found.");
            // Problems found while reading a file are reported together with these checks, so every bad row shows at once.
            var errors = earlierErrors ?? new List<string>();
            if (rows.Count == 0 && errors.Count == 0) return new SaveResult(false, 0, new List<string> { "There are no marks to save." });

            await using var c = Open();
            await c.OpenAsync();
            var allowed = (await c.QueryAsync<int>(@"SELECT DISTINCT a.studentid FROM wj_studentassociates a JOIN wj_students s ON s.studentid = a.studentid
                                                    WHERE a.instituteid = @inst AND btrim(s.standard) = @standard", new { inst, standard = exam.Standard.Trim() })).ToHashSet();
            var seen = new HashSet<int>();

            foreach (var (label, m) in rows)
            {
                if (!allowed.Contains(m.StudentId)) { errors.Add($"{label}: this student is not in {exam.Standard} of your school."); continue; }
                if (!seen.Add(m.StudentId)) { errors.Add($"{label}: this student appears twice."); continue; }
                if (!m.IsAbsent)
                {
                    if (m.MarksObtained is null) { errors.Add($"{label}: enter the marks, or mark the student absent."); continue; }
                    if (m.MarksObtained < 0 || m.MarksObtained > exam.MaxMarks) { errors.Add($"{label}: {m.MarksObtained:0.##} is outside 0 to {exam.MaxMarks:0.##}."); continue; }
                    if (decimal.Round(m.MarksObtained.Value, 2) != m.MarksObtained.Value) { errors.Add($"{label}: use at most 2 decimal places."); continue; }
                }
                if ((m.Remarks?.Length ?? 0) > 200) errors.Add($"{label}: remarks must be 200 characters or fewer.");
            }
            if (errors.Count > 0) return new SaveResult(false, 0, errors);

            await using var tx = await c.BeginTransactionAsync();
            try
            {
                await c.ExecuteAsync("SELECT pg_advisory_xact_lock(881002, @configId)", new { configId }, tx);
                foreach (var (_, m) in rows)
                {
                    var p = new { inst, configId, userId, m.StudentId, absent = m.IsAbsent ? 1 : 0, marks = m.IsAbsent ? 0 : m.MarksObtained ?? 0,
                                  remarks = string.IsNullOrWhiteSpace(m.Remarks) ? null : m.Remarks.Trim() };
                    int n = await c.ExecuteAsync(@"
                        UPDATE hf_tbl_studentmarks
                        SET marksobtained = @marks, isabsent = CAST(@absent AS bit(1)), remarks = @remarks, modifiedtimestamp = now()
                        WHERE instituteid = @inst AND studentid = @StudentId AND configid = @configId", p, tx);
                    if (n == 0)
                        await c.ExecuteAsync(@"
                            INSERT INTO hf_tbl_studentmarks (instituteid, studentid, configid, marksobtained, isabsent, remarks, createdby, createdtimestamp, modifiedtimestamp)
                            VALUES (@inst, @StudentId, @configId, @marks, CAST(@absent AS bit(1)), @remarks, @userId, now(), now())", p, tx);
                }
                await tx.CommitAsync();
                return new SaveResult(true, rows.Count, new());
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Academic: marks could not be saved for exam {Config}", configId);
                throw new AcademicException("The marks could not be saved. Nothing was changed. Please try again.");
            }
        }

        // =====================================================================
        // Excel template and upload (same layout as the old template)
        // =====================================================================
        public async Task<(byte[] File, string Name)> TemplateAsync(int inst, int configId)
        {
            var exam = await ExamAsync(inst, configId) ?? throw new AcademicException("Exam not found.");
            var students = await MarksAsync(inst, configId);
            if (students.Count == 0) throw new AcademicException($"No students of {exam.Standard} were found in your school.");

            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Marks_Template");

            ws.Cell("C1").Value = $"MARKS: {exam.Standard.ToUpperInvariant()} - {exam.TermName.ToUpperInvariant()} ({exam.AcademicYear}) - {exam.SubjectName.ToUpperInvariant()}";
            ws.Range("C1:G1").Merge().Style.Font.SetBold().Font.SetFontSize(12).Font.SetFontColor(XLColor.FromHtml("#4B2E83"));
            ws.Cell("C2").Value = $"Max marks {exam.MaxMarks:0.##} · Passing marks {exam.PassingMarks:0.##} · Write Y in “Absent” for absent students. Do not change the hidden columns.";
            ws.Range("C2:G2").Merge().Style.Font.SetItalic().Font.SetFontColor(XLColor.FromHtml("#6B6787"));

            string[] heads = { "Student ID", "Config ID", "Roll Number", "Student Name", "Marks Obtained", "Is Absent? (Y/N)", "Teacher Remarks" };
            for (int i = 0; i < heads.Length; i++) ws.Cell(3, i + 1).Value = heads[i];
            var head = ws.Range("A3:G3");
            head.Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(XLColor.FromHtml("#4B2E83"));

            int r = 4;
            foreach (var s in students)
            {
                ws.Cell(r, 1).Value = s.StudentId;
                ws.Cell(r, 2).Value = configId;
                ws.Cell(r, 3).Value = string.IsNullOrWhiteSpace(s.RollNo) ? "—" : s.RollNo;
                ws.Cell(r, 4).Value = s.Name;
                if (s.HasMark && !s.IsAbsent && s.MarksObtained is decimal mk) ws.Cell(r, 5).Value = mk;
                ws.Cell(r, 6).Value = s.HasMark && s.IsAbsent ? "Y" : "N";
                ws.Cell(r, 7).Value = s.Remarks ?? "";
                r++;
            }

            int last = r - 1;
            var marksRange = ws.Range(4, 5, last, 5);
            var dv = marksRange.CreateDataValidation();
            dv.Decimal.Between(0, (double)exam.MaxMarks);
            dv.ErrorTitle = "Marks out of range"; dv.ErrorMessage = $"Marks must be between 0 and {exam.MaxMarks:0.##}.";
            ws.Range(4, 6, last, 6).CreateDataValidation().List("\"Y,N\"");
            ws.Range(4, 1, last, 7).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetInsideBorder(XLBorderStyleValues.Hair);

            ws.Column(1).Hide(); ws.Column(2).Hide();
            ws.Column(3).Width = 14; ws.Column(4).Width = 32; ws.Column(5).Width = 16; ws.Column(6).Width = 16; ws.Column(7).Width = 34;
            ws.SheetView.FreezeRows(3);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            string Clean(string s) => Regex.Replace(s, @"[^A-Za-z0-9]+", "_").Trim('_');
            return (ms.ToArray(), $"Marks_Template_{Clean(exam.Standard)}_{Clean(exam.TermName)}_{Clean(exam.SubjectName)}.xlsx");
        }

        /// <summary>Reads the template (row 4 onwards: A student id, B exam id, E marks, F absent Y/N, G remarks).</summary>
        public async Task<SaveResult> UploadAsync(int inst, int configId, Stream file, int userId)
        {
            XLWorkbook wb;
            try { wb = new XLWorkbook(file); }
            catch { return new SaveResult(false, 0, new List<string> { "The file could not be read. Upload the .xlsx template downloaded for this exam." }); }

            using (wb)
            {
                var ws = wb.Worksheets.FirstOrDefault();
                if (ws == null || ws.LastRowUsed() == null) return new SaveResult(false, 0, new List<string> { "The file has no rows." });

                var rows = new List<(string, MarkInput)>();
                var errors = new List<string>();
                int lastRow = ws.LastRowUsed()!.RowNumber();
                for (int row = 4; row <= lastRow; row++)
                {
                    string idRaw = ws.Cell(row, 1).GetFormattedString().Trim();
                    if (string.IsNullOrEmpty(idRaw)) continue;   // blank rows at the end
                    string label = $"Row {row} ({ws.Cell(row, 4).GetFormattedString().Trim()})";

                    if (!int.TryParse(idRaw, out int studentId)) { errors.Add($"{label}: the hidden student id was changed."); continue; }
                    string cfgRaw = ws.Cell(row, 2).GetFormattedString().Trim();
                    if (cfgRaw != configId.ToString(CultureInfo.InvariantCulture)) { errors.Add($"{label}: this sheet is for a different exam. Download the template of the selected exam."); continue; }

                    string absentRaw = ws.Cell(row, 6).GetFormattedString().Trim().ToUpperInvariant();
                    bool absent = absentRaw is "Y" or "YES" or "TRUE" or "A" or "AB";
                    string marksRaw = ws.Cell(row, 5).GetFormattedString().Trim();
                    decimal? marks = null;
                    if (!absent)
                    {
                        if (string.IsNullOrEmpty(marksRaw)) { errors.Add($"{label}: marks are empty. Enter the marks or write Y in Absent."); continue; }
                        if (!decimal.TryParse(marksRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var mv)) { errors.Add($"{label}: “{marksRaw}” is not a number."); continue; }
                        marks = mv;
                    }
                    rows.Add((label, new MarkInput { StudentId = studentId, MarksObtained = marks, IsAbsent = absent, Remarks = ws.Cell(row, 7).GetFormattedString() }));
                }

                return await SaveMarksAsync(inst, configId, rows, userId, errors);
            }
        }

        // =====================================================================
        // Overview
        // =====================================================================
        public async Task<object> OverviewAsync(int inst)
        {
            var exams = await ExamsAsync(inst);
            await using var c = Open();
            var counts = await c.QuerySingleAsync<(int Subjects, int Terms, int Students)>(@"
                SELECT (SELECT count(*) FROM hf_tbl_institutesubjects WHERE instituteid = @inst AND coalesce(isactive, B'1') = B'1')::int,
                       (SELECT count(*) FROM hf_tbl_academicterms WHERE instituteid = @inst AND coalesce(isactive, B'1') = B'1')::int,
                       (SELECT count(DISTINCT studentid) FROM wj_studentassociates WHERE instituteid = @inst)::int", new { inst });

            int expected = exams.Sum(e => e.Students), entered = exams.Sum(e => e.Entered);
            int present = exams.Sum(e => e.Entered - e.Absent), passed = exams.Sum(e => e.Passed);
            return new
            {
                counts.Subjects,
                counts.Terms,
                counts.Students,
                Exams = exams.Count,
                ExamsComplete = exams.Count(e => e.Students > 0 && e.Entered >= e.Students),
                MarksEntered = entered,
                MarksExpected = expected,
                PassRate = present == 0 ? (decimal?)null : Math.Round(passed * 100m / present, 1)
            };
        }

        // =====================================================================
        // Report card
        // =====================================================================
        public record AcademicLine(string SubjectName, string? SubjectCode, int TermId, string TermName, string AcademicYear,
            decimal MaxMarks, decimal PassingMarks, decimal MarksObtained, bool IsAbsent, string? Remarks, string Status);
        public record ValueLine(string CourseName, decimal Mcq, decimal Practical, decimal Pbl)
        {
            public decimal Total => Mcq + Practical + Pbl;
        }
        public class ReportCard
        {
            public int StudentId { get; set; }
            public string StudentName { get; set; } = "";
            public string Standard { get; set; } = "";
            public string? Division { get; set; }
            public string? RollNo { get; set; }
            public int InstituteId { get; set; }
            public string InstituteName { get; set; } = "";
            public string? SchoolLogoUrl { get; set; }
            public string AcademicYear { get; set; } = "";
            public List<AcademicLine> Academic { get; set; } = new();
            public List<ValueLine> Values { get; set; } = new();
        }

        public async Task<ReportCard?> ReportCardAsync(int studentId)
        {
            await using var c = Open();
            var card = await c.QueryFirstOrDefaultAsync<ReportCard>(@"
                SELECT s.studentid AS StudentId, btrim(coalesce(u.firstname, '') || ' ' || coalesce(u.lastname, '')) AS StudentName,
                       coalesce(btrim(s.standard), '') AS Standard, s.division AS Division, s.rollno AS RollNo,
                       coalesce(a.instituteid, 0) AS InstituteId, coalesce(i.""Name"", '') AS InstituteName, iu.photourl AS SchoolLogoUrl
                FROM wj_students s
                LEFT JOIN users u ON u.userid = s.studentid
                LEFT JOIN LATERAL (SELECT instituteid FROM wj_studentassociates x WHERE x.studentid = s.studentid
                                   ORDER BY (x.isprimaryassociate::text IN ('1', 'true')) DESC LIMIT 1) a ON true
                LEFT JOIN wj_institutes i ON i.instituteid = a.instituteid
                LEFT JOIN users iu ON iu.userid = a.instituteid
                WHERE s.studentid = @studentId", new { studentId });
            if (card == null) return null;

            // Part II – academic marks (HF_SP_GetStudentHolisticAcademicReport)
            card.Academic = (await c.QueryAsync<AcademicLine>(@"
                SELECT coalesce(sub.subjectname, '') AS SubjectName, sub.subjectcode AS SubjectCode, t.termid AS TermId, coalesce(t.termname, '') AS TermName,
                       coalesce(t.academicyear, '') AS AcademicYear, coalesce(ec.maxmarks, 0) AS MaxMarks, coalesce(ec.passingmarks, 0) AS PassingMarks,
                       m.marksobtained AS MarksObtained, m.isabsent AS IsAbsent, m.remarks AS Remarks,
                       CASE WHEN m.isabsent THEN 'Absent' WHEN m.marksobtained >= coalesce(ec.passingmarks, 0) THEN 'Pass' ELSE 'Needs Improvement' END AS Status
                FROM (SELECT DISTINCT ON (configid) configid, coalesce(marksobtained, 0) AS marksobtained,
                             coalesce(isabsent = B'1', false) AS isabsent, remarks
                      FROM hf_tbl_studentmarks WHERE studentid = @studentId AND instituteid = @inst
                      ORDER BY configid, markid DESC) m
                JOIN hf_tbl_examconfigurations ec ON ec.configid = m.configid
                JOIN hf_tbl_institutesubjects sub ON sub.subjectid = ec.subjectid
                JOIN hf_tbl_academicterms t ON t.termid = ec.termid
                WHERE ec.maxmarks > 0
                ORDER BY t.academicyear DESC, t.termid, sub.subjectname", new { studentId, inst = card.InstituteId })).ToList();

            card.AcademicYear = card.Academic.Select(a => a.AcademicYear).FirstOrDefault() ?? CurrentAcademicYear(DateTime.Today);

            // Part I – value courses (SP_GetStudentActivityLogMarks): MCQ 50, Practical 30, PBL 20 per course
            const string activitySql = @"
                SELECT maincoursename AS CourseName,
                       sum(CASE WHEN activity_type = 'MCQ' THEN pts ELSE 0 END) AS Mcq,
                       sum(CASE WHEN activity_type IN ('Pratical', 'Practical', 'HEM Project') THEN pts ELSE 0 END) AS Practical,
                       sum(CASE WHEN activity_type ILIKE '%PBL%' THEN pts ELSE 0 END) AS Pbl
                FROM (SELECT maincoursename, activity_type,
                             CASE WHEN points::text ~ '^\s*-?\d+(\.\d+)?\s*$' THEN points::text::numeric ELSE 0 END AS pts
                      FROM hf_view_studentactivitylog
                      WHERE studentid = @studentId AND btrim(standard::text) = @standard AND maincoursename IS NOT NULL) x
                GROUP BY maincoursename";
            var values = (await c.QueryAsync<ValueLine>(activitySql, new { studentId, standard = card.Standard })).ToList();

            // Courses of the standard with no activity yet show 0 (the old report did the same).
            try
            {
                var done = values.Select(v => v.CourseName).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var syllabus = await c.QueryAsync<string>(@"
                    SELECT DISTINCT maincoursename FROM view_wj_syllabus
                    WHERE btrim(standard_::text) = @standard AND maincoursename IS NOT NULL
                      AND maincoursename NOT ILIKE '%Belief surv%' AND maincoursename NOT ILIKE '%behaviour sur%'", new { standard = card.Standard });
                values.AddRange(syllabus.Where(s => !done.Contains(s)).Select(s => new ValueLine(s, 0, 0, 0)));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Academic: syllabus list not available for the report card");
            }

            card.Values = values.OrderBy(v => v.CourseName).ToList();
            return card;
        }
    }
}
