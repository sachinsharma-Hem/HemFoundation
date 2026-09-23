using HemFoundation.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace HemFoundation.Controllers.Api
{
    [Route("api/dbmanager")]
    [ApiController]
    public class DbmanagerApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DbmanagerApiController> _logger;

        public DbmanagerApiController(
            ApplicationDbContext context,
            ILogger<DbmanagerApiController> logger)
        {
            _context = context;
            _logger = logger;
        }


        // =========================================================
        // AUTH CHECK
        // =========================================================

        private bool IsAdminLoggedIn()
        {
            var isLoggedIn =
                HttpContext.Session.GetString("IsAdminLoggedIn");

            return !string.IsNullOrEmpty(isLoggedIn) &&
                   isLoggedIn == "true";
        }


        // =========================================================
        // CONNECTION
        // Uses existing ApplicationDbContext database
        // =========================================================

        private NpgsqlConnection CreateConnection()
        {
            var connectionString =
                _context.Database
                    .GetDbConnection()
                    .ConnectionString;

            return new NpgsqlConnection(connectionString);
        }


        // =========================================================
        // DATABASE INFO (for the Overview cards)
        // =========================================================

        [HttpGet("database-info")]
        public async Task<IActionResult> GetDatabaseInfo()
        {
            if (!IsAdminLoggedIn())
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Admin login required."
                });
            }

            try
            {
                await using var connection = CreateConnection();

                await connection.OpenAsync();


                var version = await ExecuteScalarStringAsync(
                    connection,
                    "SELECT version();"
                );

                var databaseName = await ExecuteScalarStringAsync(
                    connection,
                    "SELECT current_database();"
                );

                var currentSchema = await ExecuteScalarStringAsync(
                    connection,
                    "SELECT current_schema();"
                );

                var sizePretty = await ExecuteScalarStringAsync(
                    connection,
                    "SELECT pg_size_pretty(pg_database_size(current_database()));"
                );

                string server;

                try
                {
                    server = $"{connection.Host}:{connection.Port}";
                }
                catch
                {
                    server = "";
                }


                // Shorten the raw "PostgreSQL 16.3 on x86_64-pc-linux-gnu, ..."
                // string down to just the version number for display.
                var shortVersion = version;

                var match = Regex.Match(
                    version,
                    @"PostgreSQL\s+([\d.]+)"
                );

                if (match.Success)
                {
                    shortVersion = "PostgreSQL " + match.Groups[1].Value;
                }


                return Ok(new
                {
                    success = true,

                    data = new
                    {
                        connected = true,
                        version = shortVersion,
                        fullVersion = version,
                        databaseName,
                        currentSchema,
                        size = sizePretty,
                        server
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error loading database info."
                );

                return StatusCode(500, new
                {
                    success = false,
                    connected = false,
                    message = ex.Message
                });
            }
        }


        // =========================================================
        // OVERVIEW
        // =========================================================

        [HttpGet("overview")]
        public async Task<IActionResult> GetOverview()
        {
            if (!IsAdminLoggedIn())
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Admin login required."
                });
            }

            try
            {
                await using var connection = CreateConnection();

                await connection.OpenAsync();


                var schemas = await ExecuteScalarLongAsync(
                    connection,
                    """
                    SELECT COUNT(*)
                    FROM information_schema.schemata
                    WHERE schema_name NOT IN
                    ('pg_catalog', 'information_schema')
                    AND schema_name NOT LIKE 'pg_%';
                    """
                );


                var tables = await ExecuteScalarLongAsync(
                    connection,
                    """
                    SELECT COUNT(*)
                    FROM information_schema.tables
                    WHERE table_type = 'BASE TABLE'
                    AND table_schema NOT IN
                    ('pg_catalog', 'information_schema')
                    AND table_schema NOT LIKE 'pg_%';
                    """
                );


                var views = await ExecuteScalarLongAsync(
                    connection,
                    """
                    SELECT COUNT(*)
                    FROM information_schema.views
                    WHERE table_schema NOT IN
                    ('pg_catalog', 'information_schema')
                    AND table_schema NOT LIKE 'pg_%';
                    """
                );


                var functions = await ExecuteScalarLongAsync(
                    connection,
                    """
                    SELECT COUNT(*)
                    FROM pg_proc p
                    INNER JOIN pg_namespace n
                        ON n.oid = p.pronamespace
                    WHERE p.prokind = 'f'
                    AND n.nspname NOT IN
                    ('pg_catalog', 'information_schema')
                    AND n.nspname NOT LIKE 'pg_%';
                    """
                );


                var procedures = await ExecuteScalarLongAsync(
                    connection,
                    """
                    SELECT COUNT(*)
                    FROM pg_proc p
                    INNER JOIN pg_namespace n
                        ON n.oid = p.pronamespace
                    WHERE p.prokind = 'p'
                    AND n.nspname NOT IN
                    ('pg_catalog', 'information_schema')
                    AND n.nspname NOT LIKE 'pg_%';
                    """
                );


                var triggers = await ExecuteScalarLongAsync(
                    connection,
                    """
                    SELECT COUNT(*)
                    FROM information_schema.triggers
                    WHERE trigger_schema NOT IN
                    ('pg_catalog', 'information_schema')
                    AND trigger_schema NOT LIKE 'pg_%';
                    """
                );


                var indexes = await ExecuteScalarLongAsync(
                    connection,
                    """
                    SELECT COUNT(*)
                    FROM pg_indexes
                    WHERE schemaname NOT IN
                    ('pg_catalog', 'information_schema')
                    AND schemaname NOT LIKE 'pg_%';
                    """
                );


                return Ok(new
                {
                    success = true,

                    data = new
                    {
                        schemas,
                        tables,
                        views,
                        functions,
                        procedures,
                        triggers,
                        indexes
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error loading database overview."
                );

                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }


        // =========================================================
        // SCHEMAS
        // =========================================================

        [HttpGet("schemas")]
        public async Task<IActionResult> GetSchemas()
        {
            if (!IsAdminLoggedIn())
                return Unauthorized();

            try
            {
                await using var connection = CreateConnection();

                await connection.OpenAsync();


                const string sql = """
                    SELECT
                        s.schema_name AS "name",
                        s.schema_owner AS "owner",
                        (
                            SELECT COUNT(*)
                            FROM information_schema.tables t
                            WHERE t.table_schema = s.schema_name
                              AND t.table_type = 'BASE TABLE'
                        ) AS "tableCount",
                        (
                            SELECT COUNT(*)
                            FROM information_schema.views v
                            WHERE v.table_schema = s.schema_name
                        ) AS "viewCount",
                        (
                            SELECT COUNT(*)
                            FROM pg_proc p
                            INNER JOIN pg_namespace n
                                ON n.oid = p.pronamespace
                            WHERE n.nspname = s.schema_name
                              AND p.prokind = 'f'
                        ) AS "functionCount"
                    FROM information_schema.schemata s
                    WHERE s.schema_name NOT IN
                    ('pg_catalog', 'information_schema')
                    AND s.schema_name NOT LIKE 'pg_%'
                    ORDER BY s.schema_name;
                    """;


                var data =
                    await ExecuteQueryAsync(
                        connection,
                        sql
                    );


                return Ok(new
                {
                    success = true,
                    data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error loading schemas."
                );

                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }


        // =========================================================
        // TABLES
        // =========================================================

        [HttpGet("tables")]
        public async Task<IActionResult> GetTables(
            [FromQuery] string? schema = null)
        {
            if (!IsAdminLoggedIn())
                return Unauthorized();

            try
            {
                await using var connection = CreateConnection();

                await connection.OpenAsync();


                const string sql = """
                    SELECT
                        t.table_schema AS "schema",
                        t.table_name AS "name",
                        COALESCE(s.n_live_tup, 0) AS "estimatedRows"
                    FROM information_schema.tables t
                    LEFT JOIN pg_stat_user_tables s
                        ON s.schemaname = t.table_schema
                       AND s.relname = t.table_name
                    WHERE t.table_type = 'BASE TABLE'
                      AND t.table_schema NOT IN
                      ('pg_catalog', 'information_schema')
                      AND t.table_schema NOT LIKE 'pg_%'
                      AND (
                          @schema IS NULL
                          OR t.table_schema = @schema
                      )
                    ORDER BY
                        t.table_schema,
                        t.table_name;
                    """;


                var data =
                    await ExecuteQueryAsync(
                        connection,
                        sql,
                        cmd =>
                        {
                            cmd.Parameters.AddWithValue(
                                "@schema",
                                (object?)schema ??
                                DBNull.Value
                            );
                        }
                    );


                return Ok(new
                {
                    success = true,
                    data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error loading tables."
                );

                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }


        // =========================================================
        // VIEWS
        // =========================================================

        [HttpGet("views")]
        public async Task<IActionResult> GetViews(
            [FromQuery] string? schema = null)
        {
            if (!IsAdminLoggedIn())
                return Unauthorized();

            try
            {
                await using var connection = CreateConnection();

                await connection.OpenAsync();


                const string sql = """
                    SELECT
                        table_schema AS "schema",
                        table_name AS "name"
                    FROM information_schema.views
                    WHERE table_schema NOT IN
                    ('pg_catalog', 'information_schema')
                    AND table_schema NOT LIKE 'pg_%'
                    AND (
                        @schema IS NULL
                        OR table_schema = @schema
                    )
                    ORDER BY
                        table_schema,
                        table_name;
                    """;


                var data =
                    await ExecuteQueryAsync(
                        connection,
                        sql,
                        cmd =>
                        {
                            cmd.Parameters.AddWithValue(
                                "@schema",
                                (object?)schema ??
                                DBNull.Value
                            );
                        }
                    );


                return Ok(new
                {
                    success = true,
                    data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error loading views."
                );

                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }


        // =========================================================
        // FUNCTIONS
        // =========================================================

        [HttpGet("functions")]
        public async Task<IActionResult> GetFunctions(
            [FromQuery] string? schema = null)
        {
            if (!IsAdminLoggedIn())
                return Unauthorized();

            try
            {
                await using var connection = CreateConnection();

                await connection.OpenAsync();


                const string sql = """
                    SELECT
                        n.nspname AS "schema",
                        p.proname AS "name",
                        pg_get_function_identity_arguments(p.oid)
                            AS "arguments",
                        pg_get_function_result(p.oid)
                            AS "returnType",
                        pg_get_functiondef(p.oid)
                            AS "definition"
                    FROM pg_proc p
                    INNER JOIN pg_namespace n
                        ON n.oid = p.pronamespace
                    WHERE p.prokind = 'f'
                      AND n.nspname NOT IN
                      ('pg_catalog', 'information_schema')
                      AND n.nspname NOT LIKE 'pg_%'
                      AND (
                          @schema IS NULL
                          OR n.nspname = @schema
                      )
                    ORDER BY
                        n.nspname,
                        p.proname;
                    """;


                var data =
                    await ExecuteQueryAsync(
                        connection,
                        sql,
                        cmd =>
                        {
                            cmd.Parameters.AddWithValue(
                                "@schema",
                                (object?)schema ??
                                DBNull.Value
                            );
                        }
                    );


                return Ok(new
                {
                    success = true,
                    data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error loading functions."
                );

                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }


        // =========================================================
        // PROCEDURES
        // =========================================================

        [HttpGet("procedures")]
        public async Task<IActionResult> GetProcedures(
            [FromQuery] string? schema = null)
        {
            if (!IsAdminLoggedIn())
                return Unauthorized();

            try
            {
                await using var connection = CreateConnection();

                await connection.OpenAsync();


                const string sql = """
                    SELECT
                        n.nspname AS "schema",
                        p.proname AS "name",
                        pg_get_function_identity_arguments(p.oid)
                            AS "arguments",
                        pg_get_functiondef(p.oid)
                            AS "definition"
                    FROM pg_proc p
                    INNER JOIN pg_namespace n
                        ON n.oid = p.pronamespace
                    WHERE p.prokind = 'p'
                      AND n.nspname NOT IN
                      ('pg_catalog', 'information_schema')
                      AND n.nspname NOT LIKE 'pg_%'
                      AND (
                          @schema IS NULL
                          OR n.nspname = @schema
                      )
                    ORDER BY
                        n.nspname,
                        p.proname;
                    """;


                var data =
                    await ExecuteQueryAsync(
                        connection,
                        sql,
                        cmd =>
                        {
                            cmd.Parameters.AddWithValue(
                                "@schema",
                                (object?)schema ??
                                DBNull.Value
                            );
                        }
                    );


                return Ok(new
                {
                    success = true,
                    data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error loading procedures."
                );

                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }


        // =========================================================
        // TRIGGERS
        // =========================================================

        [HttpGet("triggers")]
        public async Task<IActionResult> GetTriggers(
            [FromQuery] string? schema = null)
        {
            if (!IsAdminLoggedIn())
                return Unauthorized();

            try
            {
                await using var connection = CreateConnection();

                await connection.OpenAsync();


                const string sql = """
                    SELECT
                        trigger_schema AS "schema",
                        event_object_table AS "tableName",
                        trigger_name AS "name",
                        event_manipulation AS "event",
                        action_timing AS "timing",
                        action_statement AS "action"
                    FROM information_schema.triggers
                    WHERE trigger_schema NOT IN
                    ('pg_catalog', 'information_schema')
                    AND trigger_schema NOT LIKE 'pg_%'
                    AND (
                        @schema IS NULL
                        OR trigger_schema = @schema
                    )
                    ORDER BY
                        trigger_schema,
                        event_object_table,
                        trigger_name;
                    """;


                var data =
                    await ExecuteQueryAsync(
                        connection,
                        sql,
                        cmd =>
                        {
                            cmd.Parameters.AddWithValue(
                                "@schema",
                                (object?)schema ??
                                DBNull.Value
                            );
                        }
                    );


                return Ok(new
                {
                    success = true,
                    data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error loading triggers."
                );

                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }


        // =========================================================
        // INDEXES
        // =========================================================

        [HttpGet("indexes")]
        public async Task<IActionResult> GetIndexes(
            [FromQuery] string? schema = null)
        {
            if (!IsAdminLoggedIn())
                return Unauthorized();

            try
            {
                await using var connection = CreateConnection();

                await connection.OpenAsync();


                const string sql = """
                    SELECT
                        i.schemaname AS "schema",
                        i.tablename AS "tableName",
                        i.indexname AS "name",
                        i.indexdef AS "definition",
                        ix.indisunique AS "isUnique",
                        ix.indisprimary AS "isPrimary",
                        am.amname AS "method"
                    FROM pg_indexes i
                    INNER JOIN pg_class c
                        ON c.relname = i.indexname
                    INNER JOIN pg_namespace n
                        ON n.oid = c.relnamespace
                       AND n.nspname = i.schemaname
                    INNER JOIN pg_index ix
                        ON ix.indexrelid = c.oid
                    INNER JOIN pg_am am
                        ON am.oid = c.relam
                    WHERE i.schemaname NOT IN
                    ('pg_catalog', 'information_schema')
                    AND i.schemaname NOT LIKE 'pg_%'
                    AND (
                        @schema IS NULL
                        OR i.schemaname = @schema
                    )
                    ORDER BY
                        i.schemaname,
                        i.tablename,
                        i.indexname;
                    """;


                var data =
                    await ExecuteQueryAsync(
                        connection,
                        sql,
                        cmd =>
                        {
                            cmd.Parameters.AddWithValue(
                                "@schema",
                                (object?)schema ??
                                DBNull.Value
                            );
                        }
                    );


                return Ok(new
                {
                    success = true,
                    data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error loading indexes."
                );

                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }


        // =========================================================
        // TABLE STRUCTURE (columns)
        // =========================================================

        [HttpGet("table-structure")]
        public async Task<IActionResult> GetTableStructure(
            [FromQuery] string schema,
            [FromQuery] string table)
        {
            if (!IsAdminLoggedIn())
                return Unauthorized();

            if (!IsValidIdentifier(schema) ||
                !IsValidIdentifier(table))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid schema or table name."
                });
            }


            try
            {
                await using var connection = CreateConnection();

                await connection.OpenAsync();


                const string sql = """
                    SELECT
                        c.ordinal_position AS "ordinal",
                        c.column_name AS "columnName",
                        c.data_type AS "dataType",
                        c.udt_name AS "udtName",
                        c.is_nullable AS "nullable",
                        c.column_default AS "defaultValue",
                        c.character_maximum_length AS "maxLength",
                        c.numeric_precision AS "numericPrecision",
                        c.numeric_scale AS "numericScale",
                        pg_catalog.col_description(
                            format('%I.%I', c.table_schema, c.table_name)::regclass::oid,
                            c.ordinal_position
                        ) AS "comment"
                    FROM information_schema.columns c
                    WHERE c.table_schema = @schema
                      AND c.table_name = @table
                    ORDER BY c.ordinal_position;
                    """;


                var data =
                    await ExecuteQueryAsync(
                        connection,
                        sql,
                        cmd =>
                        {
                            cmd.Parameters.AddWithValue(
                                "@schema",
                                schema
                            );

                            cmd.Parameters.AddWithValue(
                                "@table",
                                table
                            );
                        }
                    );


                return Ok(new
                {
                    success = true,
                    data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error loading table structure."
                );

                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }


        // =========================================================
        // TABLE CONSTRAINTS (PK / FK / unique / check / indexes)
        // =========================================================

        [HttpGet("table-constraints")]
        public async Task<IActionResult> GetTableConstraints(
            [FromQuery] string schema,
            [FromQuery] string table)
        {
            if (!IsAdminLoggedIn())
                return Unauthorized();

            if (!IsValidIdentifier(schema) ||
                !IsValidIdentifier(table))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid schema or table name."
                });
            }

            try
            {
                await using var connection = CreateConnection();

                await connection.OpenAsync();


                // ---- Primary key / unique / foreign key constraints ----

                const string constraintsSql = """
                    SELECT
                        tc.constraint_name AS "name",
                        tc.constraint_type AS "type",
                        kcu.column_name AS "column",
                        ccu.table_schema AS "foreignSchema",
                        ccu.table_name AS "foreignTable",
                        ccu.column_name AS "foreignColumn"
                    FROM information_schema.table_constraints tc
                    LEFT JOIN information_schema.key_column_usage kcu
                        ON tc.constraint_name = kcu.constraint_name
                       AND tc.table_schema = kcu.table_schema
                    LEFT JOIN information_schema.constraint_column_usage ccu
                        ON tc.constraint_name = ccu.constraint_name
                       AND tc.constraint_type = 'FOREIGN KEY'
                       AND tc.table_schema = ccu.table_schema
                    WHERE tc.table_schema = @schema
                      AND tc.table_name = @table
                      AND tc.constraint_type IN
                          ('PRIMARY KEY', 'UNIQUE', 'FOREIGN KEY')
                    ORDER BY tc.constraint_type, tc.constraint_name;
                    """;

                var constraints = await ExecuteQueryAsync(
                    connection,
                    constraintsSql,
                    cmd =>
                    {
                        cmd.Parameters.AddWithValue("@schema", schema);
                        cmd.Parameters.AddWithValue("@table", table);
                    }
                );


                // ---- Check constraints ----

                const string checksSql = """
                    SELECT
                        con.conname AS "name",
                        pg_get_constraintdef(con.oid) AS "definition"
                    FROM pg_constraint con
                    INNER JOIN pg_class rel
                        ON rel.oid = con.conrelid
                    INNER JOIN pg_namespace nsp
                        ON nsp.oid = rel.relnamespace
                    WHERE con.contype = 'c'
                      AND nsp.nspname = @schema
                      AND rel.relname = @table
                    ORDER BY con.conname;
                    """;

                var checks = await ExecuteQueryAsync(
                    connection,
                    checksSql,
                    cmd =>
                    {
                        cmd.Parameters.AddWithValue("@schema", schema);
                        cmd.Parameters.AddWithValue("@table", table);
                    }
                );


                // ---- Indexes on this table ----

                const string indexesSql = """
                    SELECT
                        indexname AS "name",
                        indexdef AS "definition"
                    FROM pg_indexes
                    WHERE schemaname = @schema
                      AND tablename = @table
                    ORDER BY indexname;
                    """;

                var indexes = await ExecuteQueryAsync(
                    connection,
                    indexesSql,
                    cmd =>
                    {
                        cmd.Parameters.AddWithValue("@schema", schema);
                        cmd.Parameters.AddWithValue("@table", table);
                    }
                );


                return Ok(new
                {
                    success = true,

                    data = new
                    {
                        constraints,
                        checks,
                        indexes
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error loading table constraints."
                );

                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }


        // =========================================================
        // TABLE DATA
        // =========================================================

        [HttpGet("table-data")]
        public async Task<IActionResult> GetTableData(
            [FromQuery] string schema,
            [FromQuery] string table,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? search = null)
        {
            if (!IsAdminLoggedIn())
                return Unauthorized();

            if (!IsValidIdentifier(schema) ||
                !IsValidIdentifier(table))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid schema or table name."
                });
            }


            page = Math.Max(page, 1);

            pageSize = Math.Clamp(
                pageSize,
                10,
                500
            );


            try
            {
                await using var connection = CreateConnection();

                await connection.OpenAsync();


                // -------------------------------------------------
                // CHECK TABLE
                // -------------------------------------------------

                const string checkSql = """
                    SELECT EXISTS
                    (
                        SELECT 1
                        FROM information_schema.tables
                        WHERE table_schema = @schema
                          AND table_name = @table
                    );
                    """;


                await using (
                    var checkCommand =
                        new NpgsqlCommand(
                            checkSql,
                            connection
                        ))
                {
                    checkCommand.Parameters.AddWithValue(
                        "@schema",
                        schema
                    );

                    checkCommand.Parameters.AddWithValue(
                        "@table",
                        table
                    );


                    var exists =
                        Convert.ToBoolean(
                            await checkCommand
                                .ExecuteScalarAsync()
                        );


                    if (!exists)
                    {
                        return NotFound(new
                        {
                            success = false,
                            message = "Table not found."
                        });
                    }
                }


                // -------------------------------------------------
                // COLUMNS
                // -------------------------------------------------

                const string columnsSql = """
                    SELECT column_name
                    FROM information_schema.columns
                    WHERE table_schema = @schema
                      AND table_name = @table
                    ORDER BY ordinal_position;
                    """;


                var columnsResult =
                    await ExecuteQueryAsync(
                        connection,
                        columnsSql,
                        cmd =>
                        {
                            cmd.Parameters.AddWithValue(
                                "@schema",
                                schema
                            );

                            cmd.Parameters.AddWithValue(
                                "@table",
                                table
                            );
                        }
                    );


                var columns =
                    columnsResult
                        .Select(x =>
                            Convert.ToString(
                                x["column_name"]
                            ) ?? "")
                        .Where(x =>
                            !string.IsNullOrWhiteSpace(x))
                        .ToList();


                // -------------------------------------------------
                // QUOTED IDENTIFIERS
                // -------------------------------------------------

                var quotedSchema =
                    QuoteIdentifier(schema);

                var quotedTable =
                    QuoteIdentifier(table);


                // -------------------------------------------------
                // SEARCH
                // -------------------------------------------------

                string whereSql = "";


                if (!string.IsNullOrWhiteSpace(search) &&
                    columns.Count > 0)
                {
                    var searchParts =
                        columns.Select(
                            c =>
                                $"CAST({QuoteIdentifier(c)} AS TEXT) ILIKE @search"
                        );


                    whereSql =
                        " WHERE " +
                        string.Join(
                            " OR ",
                            searchParts
                        );
                }


                // -------------------------------------------------
                // COUNT
                // -------------------------------------------------

                var countSql = $"""
                    SELECT COUNT(*)
                    FROM {quotedSchema}.{quotedTable}
                    {whereSql};
                    """;


                long totalRows;


                await using (
                    var countCommand =
                        new NpgsqlCommand(
                            countSql,
                            connection
                        ))
                {
                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        countCommand.Parameters.AddWithValue(
                            "@search",
                            "%" + search.Trim() + "%"
                        );
                    }


                    totalRows =
                        Convert.ToInt64(
                            await countCommand
                                .ExecuteScalarAsync()
                        );
                }


                // -------------------------------------------------
                // DATA
                // -------------------------------------------------

                var offset =
                    (page - 1) * pageSize;


                var dataSql = $"""
                    SELECT *
                    FROM {quotedSchema}.{quotedTable}
                    {whereSql}
                    LIMIT @limit
                    OFFSET @offset;
                    """;


                var rows =
                    await ExecuteQueryAsync(
                        connection,
                        dataSql,
                        cmd =>
                        {
                            if (!string.IsNullOrWhiteSpace(search))
                            {
                                cmd.Parameters.AddWithValue(
                                    "@search",
                                    "%" + search.Trim() + "%"
                                );
                            }


                            cmd.Parameters.AddWithValue(
                                "@limit",
                                pageSize
                            );


                            cmd.Parameters.AddWithValue(
                                "@offset",
                                offset
                            );
                        }
                    );


                return Ok(new
                {
                    success = true,

                    data = rows,

                    columns,

                    pagination = new
                    {
                        page,
                        pageSize,
                        totalRows,
                        totalPages =
                            (int)Math.Ceiling(
                                totalRows /
                                (double)pageSize
                            )
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error loading table data."
                );

                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }


        // =========================================================
        // OBJECT DEFINITION
        // =========================================================

        [HttpGet("object-definition")]
        public async Task<IActionResult> GetObjectDefinition(
            [FromQuery] string type,
            [FromQuery] string schema,
            [FromQuery] string name)
        {
            if (!IsAdminLoggedIn())
                return Unauthorized();

            if (!IsValidIdentifier(schema) ||
                !IsValidIdentifier(name))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid object name."
                });
            }


            type =
                type?.Trim()
                    .ToLowerInvariant() ?? "";


            try
            {
                await using var connection = CreateConnection();

                await connection.OpenAsync();


                string sql;


                switch (type)
                {
                    case "view":

                        sql = """
                            SELECT definition
                            FROM pg_views
                            WHERE schemaname = @schema
                              AND viewname = @name
                            LIMIT 1;
                            """;

                        break;


                    case "function":

                        sql = """
                            SELECT pg_get_functiondef(p.oid)
                            FROM pg_proc p
                            INNER JOIN pg_namespace n
                                ON n.oid = p.pronamespace
                            WHERE n.nspname = @schema
                              AND p.proname = @name
                              AND p.prokind = 'f'
                            ORDER BY p.oid
                            LIMIT 1;
                            """;

                        break;


                    case "procedure":

                        sql = """
                            SELECT pg_get_functiondef(p.oid)
                            FROM pg_proc p
                            INNER JOIN pg_namespace n
                                ON n.oid = p.pronamespace
                            WHERE n.nspname = @schema
                              AND p.proname = @name
                              AND p.prokind = 'p'
                            ORDER BY p.oid
                            LIMIT 1;
                            """;

                        break;


                    case "trigger":

                        sql = """
                            SELECT
                                'Trigger: ' ||
                                trigger_name ||
                                E'\n' ||
                                'Table: ' ||
                                event_object_table ||
                                E'\n' ||
                                'Event: ' ||
                                event_manipulation ||
                                E'\n' ||
                                'Timing: ' ||
                                action_timing ||
                                E'\n' ||
                                'Action: ' ||
                                action_statement
                            FROM information_schema.triggers
                            WHERE trigger_schema = @schema
                              AND trigger_name = @name
                            LIMIT 1;
                            """;

                        break;


                    case "index":

                        sql = """
                            SELECT indexdef
                            FROM pg_indexes
                            WHERE schemaname = @schema
                              AND indexname = @name
                            LIMIT 1;
                            """;

                        break;


                    default:

                        return BadRequest(new
                        {
                            success = false,
                            message =
                                "Unsupported object type."
                        });
                }


                await using var command =
                    new NpgsqlCommand(
                        sql,
                        connection
                    );


                command.Parameters.AddWithValue(
                    "@schema",
                    schema
                );


                command.Parameters.AddWithValue(
                    "@name",
                    name
                );


                var definition =
                    await command.ExecuteScalarAsync();


                return Ok(new
                {
                    success = true,

                    data = new
                    {
                        type,
                        schema,
                        name,
                        definition =
                            definition?.ToString()
                            ?? ""
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error loading object definition."
                );

                return StatusCode(500, new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }


        // =========================================================
        // EXECUTE SQL
        //
        // Every statement is classified so the frontend terminal can
        // always show a clear, specific result instead of a generic
        // "SQL executed successfully":
        //
        //   SELECT / WITH / EXPLAIN / SHOW -> type "result" (rows)
        //   INSERT / UPDATE / DELETE       -> type "command" (affectedRows)
        //   CREATE / ALTER / DROP / etc.   -> type "command" (DDL message)
        //   anything that throws           -> type "error"
        //
        // For UPDATE statements where a table name and WHERE clause can
        // be safely identified, a follow-up SELECT is run on the SAME
        // connection (after the UPDATE has already committed) purely to
        // show the caller what the row(s) now look like. This never
        // replaces or affects the UPDATE result itself - it rides along
        // as a separate "verification" block, and if it fails for any
        // reason the original (already successful) UPDATE result is
        // still returned untouched.
        // =========================================================

        [HttpPost("execute")]
        public async Task<IActionResult> ExecuteSql(
            [FromBody] SqlRequest request)
        {
            if (!IsAdminLoggedIn())
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Admin login required."
                });
            }


            if (request == null ||
                string.IsNullOrWhiteSpace(request.Sql))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "SQL query is required."
                });
            }


            var stopwatch = Stopwatch.StartNew();

            var sql = request.Sql.Trim();


            try
            {
                await using var connection =
                    CreateConnection();

                await connection.OpenAsync();


                var firstWordMatch =
                    Regex.Match(sql, @"^(\w+)", RegexOptions.IgnoreCase);

                var command =
                    firstWordMatch.Success
                        ? firstWordMatch.Groups[1].Value.ToUpperInvariant()
                        : "SQL";

                var isSelectLike =
                    Regex.IsMatch(
                        sql,
                        @"^(SELECT|WITH|EXPLAIN|SHOW)\b",
                        RegexOptions.IgnoreCase
                    );


                // -------------------------------------------------
                // SELECT / WITH / EXPLAIN / SHOW -> rows
                // -------------------------------------------------

                if (isSelectLike)
                {
                    await using var selectCommand =
                        new NpgsqlCommand(sql, connection)
                        {
                            CommandTimeout = 120
                        };

                    var (columns, rows) =
                        await ReadAllAsync(selectCommand);

                    stopwatch.Stop();

                    return Ok(new
                    {
                        success = true,
                        type = "result",
                        command,
                        message = $"{command} executed successfully.",
                        columns,
                        data = rows,
                        rowCount = rows.Count,
                        executionTimeMs = stopwatch.ElapsedMilliseconds,

                        // kept for compatibility with older frontend builds
                        elapsedMs = stopwatch.ElapsedMilliseconds
                    });
                }


                // -------------------------------------------------
                // Everything else -> DML / DDL, no result rows
                // -------------------------------------------------

                await using var execCommand =
                    new NpgsqlCommand(sql, connection)
                    {
                        CommandTimeout = 120
                    };

                var affectedRows =
                    await execCommand.ExecuteNonQueryAsync();

                stopwatch.Stop();


                // Work out which kind of object this touched (view,
                // function, procedure, trigger, index, table, schema)
                // so the frontend knows which section to auto-refresh.

                var kindMatch = Regex.Match(
                    sql,
                    @"^(?:CREATE|ALTER|DROP)\s+(?:OR\s+REPLACE\s+)?(?:UNIQUE\s+)?(MATERIALIZED\s+VIEW|VIEW|FUNCTION|PROCEDURE|TRIGGER|INDEX|TABLE|SCHEMA)\b",
                    RegexOptions.IgnoreCase
                );

                var objectKind =
                    kindMatch.Success
                        ? Regex.Replace(kindMatch.Groups[1].Value, @"\s+", " ").ToUpperInvariant()
                        : "";

                var isReplace =
                    Regex.IsMatch(sql, @"^\s*CREATE\s+OR\s+REPLACE", RegexOptions.IgnoreCase);


                string message;
                var commandDisplay = command;


                if (command is "INSERT" or "UPDATE" or "DELETE")
                {
                    message = $"{command} executed successfully.";
                }
                else if (!string.IsNullOrEmpty(objectKind))
                {
                    commandDisplay = $"{command} {objectKind}";

                    message = command switch
                    {
                        "CREATE" when isReplace => $"{TitleCase(objectKind)} updated successfully.",
                        "CREATE" => $"{TitleCase(objectKind)} created successfully.",
                        "DROP" => $"{TitleCase(objectKind)} dropped successfully.",
                        "ALTER" => $"{TitleCase(objectKind)} altered successfully.",
                        _ => $"{commandDisplay} executed successfully."
                    };
                }
                else
                {
                    message = $"{command} command executed successfully.";
                }


                // -------------------------------------------------
                // Best-effort verification query, UPDATE only
                // -------------------------------------------------

                object? verification = null;

                if (command == "UPDATE")
                {
                    var extracted = TryExtractTableAndWhere(sql, "UPDATE");

                    if (extracted != null)
                    {
                        try
                        {
                            var verifySql =
                                $"SELECT * FROM {extracted.Value.Table} WHERE {extracted.Value.Where} LIMIT 100;";

                            await using var verifyCommand =
                                new NpgsqlCommand(verifySql, connection)
                                {
                                    CommandTimeout = 30
                                };

                            var (vColumns, vRows) =
                                await ReadAllAsync(verifyCommand);

                            verification = new
                            {
                                columns = vColumns,
                                data = vRows,
                                rowCount = vRows.Count
                            };
                        }
                        catch
                        {
                            // Verification is a convenience only - never let
                            // it turn an already-successful UPDATE into an
                            // error response.
                            verification = null;
                        }
                    }
                }


                return Ok(new
                {
                    success = true,
                    type = "command",
                    command = commandDisplay,
                    objectKind = objectKind.ToLowerInvariant(),
                    message,
                    affectedRows,
                    executionTimeMs = stopwatch.ElapsedMilliseconds,
                    verification,

                    // kept for compatibility with older frontend builds
                    elapsedMs = stopwatch.ElapsedMilliseconds
                });
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                _logger.LogError(
                    ex,
                    "Database SQL execution failed."
                );


                return StatusCode(500, new
                {
                    success = false,
                    type = "error",
                    message = ex.Message,
                    executionTimeMs = stopwatch.ElapsedMilliseconds,

                    // kept for compatibility with older frontend builds
                    elapsedMs = stopwatch.ElapsedMilliseconds
                });
            }
        }


        // =========================================================
        // HELPER - READ ALL ROWS FROM A COMMAND
        // =========================================================

        private static async Task<
            (List<string> Columns, List<Dictionary<string, object?>> Rows)
        > ReadAllAsync(NpgsqlCommand command)
        {
            var columns = new List<string>();

            var rows =
                new List<Dictionary<string, object?>>();


            await using var reader =
                await command.ExecuteReaderAsync();


            for (int i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(reader.GetName(i));
            }


            while (await reader.ReadAsync())
            {
                var row =
                    new Dictionary<string, object?>(
                        StringComparer.OrdinalIgnoreCase
                    );

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] =
                        reader.IsDBNull(i)
                            ? null
                            : reader.GetValue(i);
                }

                rows.Add(row);
            }


            return (columns, rows);
        }


        // =========================================================
        // HELPER - EXTRACT "table" + "WHERE clause" FROM
        // UPDATE / DELETE, for the safe post-UPDATE verification query.
        // Returns null whenever extraction isn't unambiguous, so the
        // caller simply skips verification rather than guessing.
        // =========================================================

        private static (string Table, string Where)? TryExtractTableAndWhere(
            string sql,
            string command)
        {
            var pattern =
                command == "UPDATE"
                    ? @"^UPDATE\s+([^\s]+)\s+SET\s+.*?\bWHERE\b\s+(.*?)\s*;?\s*$"
                    : @"^DELETE\s+FROM\s+([^\s]+)\s+.*?\bWHERE\b\s+(.*?)\s*;?\s*$";

            var match = Regex.Match(
                sql,
                pattern,
                RegexOptions.IgnoreCase | RegexOptions.Singleline
            );

            if (!match.Success)
                return null;

            var table = match.Groups[1].Value.Trim();
            var where = match.Groups[2].Value.Trim();

            if (string.IsNullOrWhiteSpace(table) ||
                string.IsNullOrWhiteSpace(where))
                return null;

            // If a second statement snuck in, don't guess - skip verification.
            if (where.Contains(';'))
                return null;

            return (table, where);
        }


        // =========================================================
        // HELPER - TITLE CASE (for messages like "View updated successfully.")
        // =========================================================

        private static string TitleCase(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            var lower = value.ToLowerInvariant();

            return char.ToUpperInvariant(lower[0]) + lower.Substring(1);
        }


        // =========================================================
        // HELPER - QUERY
        // =========================================================

        private static async Task<
            List<Dictionary<string, object?>>
        > ExecuteQueryAsync(
            NpgsqlConnection connection,
            string sql,
            Action<NpgsqlCommand>? configure = null)
        {
            await using var command =
                new NpgsqlCommand(
                    sql,
                    connection
                );


            configure?.Invoke(command);


            var result =
                new List<
                    Dictionary<string, object?>
                >();


            await using var reader =
                await command
                    .ExecuteReaderAsync();


            while (await reader.ReadAsync())
            {
                var row =
                    new Dictionary<
                        string,
                        object?
                    >(
                        StringComparer
                            .OrdinalIgnoreCase
                    );


                for (
                    int i = 0;
                    i < reader.FieldCount;
                    i++)
                {
                    row[
                        reader.GetName(i)
                    ] =
                        reader.IsDBNull(i)
                            ? null
                            : reader.GetValue(i);
                }


                result.Add(row);
            }


            return result;
        }


        // =========================================================
        // HELPER - SCALAR (long)
        // =========================================================

        private static async Task<long>
            ExecuteScalarLongAsync(
                NpgsqlConnection connection,
                string sql)
        {
            await using var command =
                new NpgsqlCommand(
                    sql,
                    connection
                );


            var value =
                await command
                    .ExecuteScalarAsync();


            return Convert.ToInt64(value);
        }


        // =========================================================
        // HELPER - SCALAR (string)
        // =========================================================

        private static async Task<string>
            ExecuteScalarStringAsync(
                NpgsqlConnection connection,
                string sql)
        {
            await using var command =
                new NpgsqlCommand(
                    sql,
                    connection
                );


            var value =
                await command
                    .ExecuteScalarAsync();


            return value?.ToString() ?? "";
        }


        // =========================================================
        // HELPER - IDENTIFIER VALIDATION
        // =========================================================

        private static bool IsValidIdentifier(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;


            return Regex.IsMatch(
                value,
                @"^[A-Za-z_][A-Za-z0-9_$]*$"
            );
        }


        // =========================================================
        // HELPER - QUOTE IDENTIFIER
        // =========================================================

        private static string QuoteIdentifier(
            string identifier)
        {
            if (!IsValidIdentifier(identifier))
            {
                throw new ArgumentException(
                    "Invalid PostgreSQL identifier."
                );
            }


            return "\"" +
                   identifier.Replace(
                       "\"",
                       "\"\""
                   ) +
                   "\"";
        }
    }


    // =============================================================
    // SQL REQUEST
    // =============================================================

    public class SqlRequest
    {
        public string Sql { get; set; } = string.Empty;
    }
}