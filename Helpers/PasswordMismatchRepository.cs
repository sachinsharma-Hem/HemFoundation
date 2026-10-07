using Dapper;
using HemFoundation.Models.Admin;
using HemFoundation.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Npgsql;

namespace HemFoundation.Helpers
{
    public interface IPasswordMismatchRepository
    {
        Task<List<PasswordMismatchUserDto>> GetMismatchUsersAsync();

        Task<(List<PasswordMismatchUserDto> Users, int TotalCount)> GetMismatchUsersPagedAsync(
            int pageNumber,
            int pageSize);

        Task<List<PasswordFixResultDto>> FixPasswordsAsync(
            List<string> userIds);
    }

    public class PasswordMismatchRepository : IPasswordMismatchRepository
    {
        private readonly IConfiguration _configuration;
        private readonly IPasswordHasher<IdentityUser> _passwordHasher;

        public PasswordMismatchRepository(
            IConfiguration configuration,
            IPasswordHasher<IdentityUser> passwordHasher)
        {
            _configuration = configuration;
            _passwordHasher = passwordHasher;
        }

        private string ConnectionString =>
            _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "DefaultConnection is not configured.");

        public async Task<List<PasswordMismatchUserDto>> GetMismatchUsersAsync()
        {
            // Fallback implementation if needed
            var (users, _) = await GetMismatchUsersPagedAsync(1, 50);
            return users;
        }

        public async Task<(List<PasswordMismatchUserDto> Users, int TotalCount)> GetMismatchUsersPagedAsync(int pageNumber, int pageSize)
        {
            using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();

            // 1. Get Total Count first
            const string countSql = @"
                SELECT COUNT(1)
                FROM Users u
                INNER JOIN aspnetusers au ON u.aspnetuserid = au.id
                WHERE u.password_ IS NOT NULL
                  AND LTRIM(RTRIM(u.password_)) <> ''
                  AND au.PasswordHash IS NOT NULL;
            ";
            int totalCount = await connection.ExecuteScalarAsync<int>(countSql);

            // 2. Fetch only the paged subset using OFFSET / FETCH
            const string pagedSql = @"
                SELECT
                    u.aspnetuserid AS UserId,
                    au.userName,
                    u.emailid AS Email,
                    u.password_ AS PlainPassword,
                    au.PasswordHash
                FROM Users u
                INNER JOIN aspnetusers au
                    ON u.aspnetuserid = au.id
                WHERE
                    u.password_ IS NOT NULL
                    AND LTRIM(RTRIM(u.password_)) <> ''
                    AND au.PasswordHash IS NOT NULL
                ORDER BY u.userid
                OFFSET @Offset ROWS 
                FETCH NEXT @PageSize ROWS ONLY;
            ";

            int offset = (pageNumber - 1) * pageSize;
            var users = await connection.QueryAsync<UserPasswordInternal>(pagedSql, new { Offset = offset, PageSize = pageSize });

            var mismatchUsers = new List<PasswordMismatchUserDto>();

            foreach (var user in users)
            {
                if (string.IsNullOrEmpty(user.PasswordHash))
                    continue;

                var identityUser = new IdentityUser
                {
                    Id = user.UserId,
                    UserName = user.UserName,
                    Email = user.Email
                };

                var result = _passwordHasher.VerifyHashedPassword(
                    identityUser,
                    user.PasswordHash,
                    user.PlainPassword);

                if (result == PasswordVerificationResult.Failed)
                {
                    mismatchUsers.Add(
                        new PasswordMismatchUserDto
                        {
                            UserId = user.UserId,
                            UserName = user.UserName,
                            Email = user.Email,
                            Status = "Password Mismatch"
                        });
                }
            }

            return (mismatchUsers, totalCount);
        }

        public async Task<List<PasswordFixResultDto>> FixPasswordsAsync(List<string> userIds)
        {
            var results = new List<PasswordFixResultDto>();

            if (userIds == null || userIds.Count == 0)
                return results;

            using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();

            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                const string selectSql = @"
                    SELECT
                        CAST(u.aspnetuserid AS nvarchar(450)) AS UserId,
                        u.userName,
                        u.emailid AS Email,
                        u.password_ AS PlainPassword,
                        au.PasswordHash
                    FROM Users u
                    INNER JOIN aspnetusers au
                        ON CAST(u.aspnetuserid AS nvarchar(450)) = CAST(au.id AS nvarchar(450))
                    WHERE
                        CAST(u.aspnetuserid AS nvarchar(450)) IN @UserIds
                        AND u.password_ IS NOT NULL
                        AND LTRIM(RTRIM(u.password_)) <> '';
                ";

                var users = await connection.QueryAsync<UserPasswordInternal>(
                    selectSql,
                    new { UserIds = userIds },
                    transaction);

                var processedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var user in users)
                {
                    processedIds.Add(user.UserId);

                    try
                    {
                        var identityUser = new IdentityUser
                        {
                            Id = user.UserId,
                            UserName = user.UserName,
                            Email = user.Email
                        };

                        if (string.IsNullOrEmpty(user.PasswordHash))
                        {
                            results.Add(
                                new PasswordFixResultDto
                                {
                                    UserId = user.UserId,
                                    UserName = user.UserName,
                                    Success = false,
                                    Message = "PasswordHash is empty."
                                });
                            continue;
                        }

                        var verificationResult = _passwordHasher.VerifyHashedPassword(
                            identityUser,
                            user.PasswordHash,
                            user.PlainPassword);

                        if (verificationResult != PasswordVerificationResult.Failed)
                        {
                            results.Add(
                                new PasswordFixResultDto
                                {
                                    UserId = user.UserId,
                                    UserName = user.UserName,
                                    Success = true,
                                    Message = "Password already matches. No update required."
                                });
                            continue;
                        }

                        var newPasswordHash = _passwordHasher.HashPassword(
                            identityUser,
                            user.PlainPassword);

                        const string updateSql = @"
                            UPDATE aspnetusers
                            SET PasswordHash = @PasswordHash
                            WHERE CAST(id AS nvarchar(450)) = @UserId;
                        ";

                        var affected = await connection.ExecuteAsync(
                            updateSql,
                            new
                            {
                                PasswordHash = newPasswordHash,
                                UserId = user.UserId
                            },
                            transaction);

                        if (affected == 1)
                        {
                            results.Add(
                                new PasswordFixResultDto
                                {
                                    UserId = user.UserId,
                                    UserName = user.UserName,
                                    Success = true,
                                    Message = "Password hash updated successfully."
                                });
                        }
                        else
                        {
                            results.Add(
                                new PasswordFixResultDto
                                {
                                    UserId = user.UserId,
                                    UserName = user.UserName,
                                    Success = false,
                                    Message = "aspnetusers record was not updated."
                                });
                        }
                    }
                    catch (Exception ex)
                    {
                        results.Add(
                            new PasswordFixResultDto
                            {
                                UserId = user.UserId,
                                UserName = user.UserName,
                                Success = false,
                                Message = ex.Message
                            });
                    }
                }

                await transaction.CommitAsync();

                // Check for requested IDs that were not found in the database
                foreach (var requestedId in userIds)
                {
                    if (!processedIds.Contains(requestedId))
                    {
                        results.Add(
                            new PasswordFixResultDto
                            {
                                UserId = requestedId,
                                Success = false,
                                Message = "User was not found or does not have a valid password."
                            });
                    }
                }
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return results;
        }

        private class UserPasswordInternal
        {
            public string UserId { get; set; } = string.Empty;
            public string? UserName { get; set; }
            public string? Email { get; set; }
            public string PlainPassword { get; set; } = string.Empty;
            public string? PasswordHash { get; set; }
        }
    }
}