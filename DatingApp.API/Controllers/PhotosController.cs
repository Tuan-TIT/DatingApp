using AutoMapper;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using DatingApp.API.Data;
using DatingApp.API.Dtos;
using DatingApp.API.Helpers;
using DatingApp.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DatingApp.API.Controllers
{
    [Authorize]
    [Route("api/users/{userId}/photos")]
    [ApiController]
    public class PhotosController : ControllerBase
    {
        private readonly IDatingRepository _dating;
        private readonly IMapper _mapper;
        private readonly IOptions<CloudinarySettings> _cloudinaryOptions;

        private Cloudinary _cloudinary;

        public PhotosController(IDatingRepository dating, IMapper mapper, IOptions<CloudinarySettings> cloudinaryOptions)
        {
            _dating = dating;
            _mapper = mapper;
            _cloudinaryOptions = cloudinaryOptions;

            Account acc = new Account(
                _cloudinaryOptions.Value.CloudName,
                _cloudinaryOptions.Value.ApiKey,
                _cloudinaryOptions.Value.ApiSecret
            );
            _cloudinary = new Cloudinary(acc);
        }

        [HttpGet("{id}", Name = "GetPhoto")]
        public async Task<IActionResult> GetPhoto (int id)
        {
            var photo = await _dating.GetPhoto(id);

            var photoDto = _mapper.Map<PhotoForReturnDto>(photo);
            return Ok(photoDto);
        }


        [HttpPost]
        public async Task<IActionResult> AddPhotoForUser(int userId, [FromForm]PhotoCreationDto photoCreate)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
                return Unauthorized();

            var userFromRepo = await _dating.GetUser(userId);

            var file = photoCreate.File;

            var uploadResult = new ImageUploadResult();

            if (file.Length > 0)
            {
                using (var stream = file.OpenReadStream())
                {
                    var uploadParam = new ImageUploadParams
                    {
                        File = new FileDescription(file.Name, stream),
                        Transformation = new Transformation().Width(500)
                                                .Height(500).Crop("fill").Gravity("face")
                    };

                    uploadResult = _cloudinary.Upload(uploadParam);
                }
            }

            photoCreate.Url = uploadResult.Uri.ToString();
            photoCreate.PublicId = uploadResult.PublicId.ToString();

            var photo = _mapper.Map<Photo>(photoCreate);

            if (!userFromRepo.Photos.Any(s => s.IsMain))
                photo.IsMain = true;
            userFromRepo.Photos.Add(photo);

            
            if (await _dating.SaveAll())
            {
                var photoToReturn = _mapper.Map<PhotoForReturnDto>(photo);
                return CreatedAtRoute("GetPhoto", new { id = photo.Id }, photoToReturn);
            }
               
            return BadRequest("Could not add the photo");
        }

        [HttpPost("{id}/setMain")]
        public async Task<IActionResult> SetMainPhotos(int userId, int id)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
                return Unauthorized();

            var user = await _dating.GetUser(userId);

            if (!user.Photos.Any(s => s.Id == id))
                return Unauthorized();
            var photoFromRepo = await _dating.GetPhoto(id);
            if (photoFromRepo.IsMain)
                return BadRequest("This photo is already the main photo");

            var currentMain =await _dating.GetMainPhotoUser(userId);
            if (currentMain != null) currentMain.IsMain = false;

            photoFromRepo.IsMain = true;
            if (await _dating.SaveAll())
                return NoContent();

            return BadRequest("Could not set photo is main");
        }

        [HttpPost("{id}/deletePhoto")]
        public async Task<IActionResult> DeletePhoto(int userId, int id)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
                return Unauthorized();

            var user = await _dating.GetUser(userId);

            if (!user.Photos.Any(s => s.Id == id))
                return Unauthorized();
            var photoFromRepo = await _dating.GetPhoto(id);
            if (photoFromRepo.IsMain)
                return BadRequest("You can not delete main photo");

            var deleteParams = new DeletionParams(photoFromRepo.PublicId);
            var rs = _cloudinary.Destroy(deleteParams);
            if(rs.Result == "ok")
            {
                _dating.Delete(photoFromRepo);
                if (await _dating.SaveAll())
                    return NoContent();
            }

            return BadRequest("You can not delete this photo");
        }
    }
}
