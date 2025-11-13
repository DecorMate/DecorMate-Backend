using AutoMapper;
using DecorMate_Backend_Web_app.Data;
using DecorMate_Backend_Web_app.Models;
using DecorMate_Backend_Web_app.Services;
using DecorMateBackend.Models;
using DecorMateBackend.Models.DTOs;
using DecorMateBackend.Repositories;
using DecorMateBackend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Linq;

namespace DecorMateBackend.Controllers
{
    [Route("api/Business")]
    [ApiController]
    public class BusinessController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        
        public BusinessController(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet("filter")]
        public async Task<IActionResult> Filter([FromQuery] VendorFilterRequestDto q)
        {
            // join users with roles to select only vendors (companies / professionals)
            // We assume users with role "Company" or "Vendor" etc. Use role names your app uses.
            var vendorRoleNames = new[] { "Company", "Vendor", "Manufacturer", "Designer" }; // tune as needed

            var users = await _unitOfWork.Users.GetUsersByRolesAsync(vendorRoleNames);
            var usersQ = users.AsQueryable();
            // get role ids for these role names


            if (!string.IsNullOrWhiteSpace(q.Location))
            {
                var loc = q.Location.Trim().ToLower();
                usersQ = usersQ.Where(u => (u.CompanyName ?? "").ToLower().Contains(loc)
                                            || (u.ProfilePictureUrl ?? "").ToLower().Contains(loc) == false // placeholder (optional)
                                            || (u.CompanyName ?? "").ToLower().Contains(loc) // keep for search
                                            || (u.CompanyName ?? "").ToLower().Contains(loc) // duplicate safe
                                           );
                // ideally user should have a Location field; if so use it:
                usersQ = usersQ.Where(u => (u.Location ?? "").ToLower().Contains(loc) || (u.CompanyName ?? "").ToLower().Contains(loc));
            }

            if (!string.IsNullOrWhiteSpace(q.Category))
            {
                var cat = q.Category.Trim().ToLower();
                usersQ = usersQ.Where(u => (u.CompanyName ?? "").ToLower().Contains(cat));
            }

            if (!string.IsNullOrWhiteSpace(q.Search))
            {
                var s = q.Search.Trim().ToLower();
                usersQ = usersQ.Where(u =>
                    (u.FirstName ?? "").ToLower().Contains(s)
                    || (u.LastName ?? "").ToLower().Contains(s)
                    || (u.CompanyName ?? "").ToLower().Contains(s)
                    || (u.Email ?? "").ToLower().Contains(s)
                );
            }

            // Project to DTO plus ratings aggregation and sponsorship priority
            var page = Math.Max(1, q.Page);
            var pageSize = Math.Clamp(q.PageSize, 1, 100);

            var baseList = usersQ.ToList();

            // sort: sponsored first, then by avg rating desc, then by ratings count desc
            var vendorDtos = baseList.Select(u =>
            {
                var dto = _mapper.Map<VendorDto>(u);
                var ratingsAverage = _unitOfWork.Vendors.GetAverageRatingsOfVendor(u.Id);
                dto.AverageRating = Math.Round(ratingsAverage, 2);
                dto.RatingsCount = _unitOfWork.Vendors.CountRatings(u.Id);
                return dto;
            }).ToList();

            var ordered = vendorDtos
                .OrderByDescending(x => x.IsSponsored ? 1 : 0)
                .ThenByDescending(x => x.AverageRating)
                .ThenByDescending(x => x.RatingsCount)
                .ToList();

            var total = ordered.Count;
            var paged = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var resp = new
            {
                Total = total,
                Page = page,
                PageSize = pageSize,
                Items = paged
            };

            return Ok(resp);
        }

        // POST api/vendors/{vendorId}/rate
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost("{vendorId}/rate")]
        public async Task<IActionResult> RateVendor([FromRoute] string vendorId, [FromBody] RateVendorDto dto)
        {
            if (string.IsNullOrEmpty(vendorId)) return BadRequest(new { message = "vendorId required" });
            if (dto.Score < 1 || dto.Score > 5) return BadRequest(new { message = "score must be 1..5" });

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var vendor = await _unitOfWork.Users.FindByIdAsync(vendorId);
            if (vendor == null)
                return NotFound(new { message = "Vendor not found" });

            // check if rater is trying to rate self
            if (vendorId == userId)
                return BadRequest(new { message = "You cannot rate yourself" });

            // See if an existing rating by this user exists — update it; otherwise create new
            var existing = await _unitOfWork.Vendors.GetVendorRateByUserAsync(vendorId, userId);
            if (existing != null)
            {
                existing.Score = dto.Score;
                existing.Comment = dto.Comment;
                existing.CreatedAt = DateTime.UtcNow;
                _unitOfWork.Vendors.UpdateRating(existing);
            }
            else
            {
                var r = new VendorRating
                {
                    ApplicationUserId = vendorId,
                    RatedByUserId = userId,
                    Score = dto.Score,
                    Comment = dto.Comment,
                    CreatedAt = DateTime.UtcNow
                };
                await _unitOfWork.Vendors.AddRatingAsync(r);
            }

            await _unitOfWork.SaveChangesAsync();

            // return updated aggregated info
            var AverageAndTotalRating = await _unitOfWork.Vendors.GetAverageRatingAndTotalRatingsAsync(vendorId);

            return Ok(new { AverageAndTotalRating });
        }
    }
}
