using Microsoft.AspNetCore.Mvc;
using PDF_CRUD.Data;
using PDF_CRUD.DTO;
using PDF_CRUD.Model;
using PDF_CRUD.Service;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace PDF_CRUD.Controllers
{

    [ApiController]
    [Route("[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public UsersController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // Create User
        [HttpPost("CreateUser")]

        public async Task<ActionResult<User>> CreateUser([FromForm] UserDto userDto)
        {
            var user = new User
            {
                Name = userDto.Name,
                ImagePath = await SaveFile(userDto.ImageFile, "images"),
                PdfPath = await SaveFile(userDto.PdfFile, "pdfs")
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
        }

        // Get User
        [HttpGet("GetUser/{id}")]
        public async Task<ActionResult<User>> GetUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            return user;
        }

        // Update User
        [HttpPut("Update/{id}")]

        public async Task<ActionResult<User>> UpdateUser(int id, [FromForm] UserDto userDto)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            user.Name = userDto.Name;

            // Handle image update
            if (userDto.ImageFile != null)
            {
                // Delete old image if exists

                if (!await AreFilesIdentical(userDto.ImageFile, Path.Combine(_env.WebRootPath, user.ImagePath)))
                {

                    if (!string.IsNullOrEmpty(user.ImagePath))
                    {
                        var oldImagePath = Path.Combine(_env.WebRootPath, user.ImagePath);
                        if (System.IO.File.Exists(oldImagePath))
                        {
                            System.IO.File.Delete(oldImagePath);
                        }
                    }
                    // Save new image
                    user.ImagePath = await SaveFile(userDto.ImageFile, "Images");
                }
            }
            // Handle PDF update
            if (userDto.PdfFile != null)
            {
                if (!await AreFilesIdentical(userDto.PdfFile, Path.Combine(_env.WebRootPath, user.PdfPath)))
                {

                    // Delete old PDF if exists

                    if (!string.IsNullOrEmpty(user.PdfPath))
                    {
                        var oldPdfPath = Path.Combine(_env.WebRootPath, user.PdfPath);
                        if (System.IO.File.Exists(oldPdfPath))
                        {
                            System.IO.File.Delete(oldPdfPath);
                        }
                    }
                    // Save new PDF
                    user.PdfPath = await SaveFile(userDto.PdfFile, "PDF's");
                }
            }


            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            return Ok(user);
 }

        // Helper methods
        private async Task<string> SaveFile(IFormFile file, string folder)
        {
            if (file == null || file.Length == 0) return null;

            var uploads = Path.Combine(_env.WebRootPath, "uploads", folder);
            if (!Directory.Exists(uploads))
            {
                Directory.CreateDirectory(uploads); // Creates if doesn't exist
            }


            // var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            var fileName = file.FileName;
            var filePath = Path.Combine(uploads, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return Path.Combine("uploads", folder, fileName);
        }

        [HttpGet("edit/{id}")]
        public async Task<ActionResult<UserEditDto>> GetUserForEdit(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            return new UserEditDto
            {
                Id = user.Id,
                Name = user.Name,
                ExistingImagePath = user.ImagePath,
                ExistingPdfPath = user.PdfPath
            };
        }

        private void DeleteFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            var fullPath = Path.Combine(_env.WebRootPath, filePath);
            if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
        }

        private async Task<bool> AreFilesIdentical(IFormFile newFile, string existingFilePath)
        {
            if (string.IsNullOrEmpty(existingFilePath) || newFile == null)
                return false;

            var newFileHash = await GetFileHash(newFile);
            var existingFileHash = await GetFileHash(existingFilePath);

            return newFileHash == existingFileHash;
        }

        private async Task<string> GetFileHash(object file)
        {
            using var sha256 = SHA256.Create();
            {
                Stream stream = null;
                if (file is IFormFile formFile)
                {
                    stream = formFile.OpenReadStream();
                }
                else if (file is string filePath)
                {
                    stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                }
                var hash = await sha256.ComputeHashAsync(stream);
                stream.Dispose();
                return BitConverter.ToString(hash).Replace("-", "").ToLower();

            }
        }

        [HttpGet("GetImageStream/{id}")]
        public async Task<IActionResult> GetImageStream(int id)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null || string.IsNullOrEmpty(user.ImagePath))
                {
                    return NotFound("User or image not found");
                }

                var imagePath = Path.Combine(_env.WebRootPath, user.ImagePath);
                if (!System.IO.File.Exists(imagePath))
                {
                    return NotFound("Image file not found");
                }

                // Determine content type from file extension
                var contentType = GetContentType(imagePath);

                // Return the file stream
                var fileStream = System.IO.File.OpenRead(imagePath);
                return File(fileStream, contentType, enableRangeProcessing: true);
            }
            catch (Exception ex)
            {
              
                return StatusCode(500, "Internal server error");
            }
        }

        private string GetContentType(string path)
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
        }

        //private async Task<string> GetFileHash(IFormFile file)
        //{
        //    using (var sha256 = SHA256.Create())
        //    {
        //        using (var stream = file.OpenReadStream())
        //        {
        //            var hash = await sha256.ComputeHashAsync(stream);
        //            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        //        }
        //    }
        //}

        //private async Task<string> GetFileHash(string filePath)
        //{
        //    using (var sha256 = SHA256.Create())
        //    {
        //        using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        //        {
        //            var hash = await sha256.ComputeHashAsync(stream);
        //            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        //        }
        //    }
        //}

    }
}

