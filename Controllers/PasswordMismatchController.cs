using HemFoundation.Helpers;
using HemFoundation.Models.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HemFoundation.Models.Admin;

namespace HemFoundation.Controllers
{
   
    [Route("api/admin/password-mismatch")]
    [ApiController]
    public class PasswordMismatchController : ControllerBase
    {
        private readonly IPasswordMismatchRepository _repository;
        private readonly ILogger<PasswordMismatchController> _logger;

        public PasswordMismatchController(
            IPasswordMismatchRepository repository,
            ILogger<PasswordMismatchController> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetMismatchUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 150)
        {
            try
            {
                // Safety cap on page size
               // if (pageSize > 200) pageSize = 200;

                var (users, totalCount) = await _repository.GetMismatchUsersPagedAsync(page, pageSize);

                return Ok(new
                {
                    success = true,
                    page = page,
                    pageSize = pageSize,
                    totalCount = totalCount,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                    data = users
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while checking password mismatches.");
                return StatusCode(500, new { success = false, message = "Unable to check password mismatches." });
            }
        }

        [HttpPost("fix")]
        public async Task<IActionResult> FixPasswords(
            [FromBody] FixPasswordRequest request)
        {
            if (request == null ||
                request.UserIds == null ||
                request.UserIds.Count == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "Please select at least one user."
                });
            }

            /*
             * Remove duplicate IDs.
             */

            request.UserIds =
                request.UserIds
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .ToList();

            /*
             * Optional safety limit.
             * You can increase this if required.
             */

            if (request.UserIds.Count > 500)
            {
                return BadRequest(new
                {
                    success = false,
                    message =
                        "You can update a maximum of 500 users at a time."
                });
            }

            try
            {
                var results =
                    await _repository.FixPasswordsAsync(
                        request.UserIds);

                return Ok(new
                {
                    success = true,
                    total = results.Count,
                    updated = results.Count(x =>
                        x.Success &&
                        x.Message.Contains(
                            "updated successfully",
                            StringComparison.OrdinalIgnoreCase)),
                    results
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error while fixing password hashes.");

                return StatusCode(500, new
                {
                    success = false,
                    message =
                        "Unable to update password hashes."
                });
            }
        }

       
    }
}