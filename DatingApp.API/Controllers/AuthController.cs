using AutoMapper;
using DatingApp.API.Data;
using DatingApp.API.Dtos;
using DatingApp.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace DatingApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthRepository _auth;
        private readonly IConfiguration _config;
        private readonly IMapper _mapper;

        public AuthController(IAuthRepository auth, IConfiguration config, IMapper mapper)
        {
            _auth = auth;
            _config = config;
            _mapper = mapper;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(UserDto userDto)
        {
            userDto.Username = userDto.Username.ToLower();
            if (await _auth.UserExists(userDto.Username))
                return BadRequest("Username already exists");
            var user = _mapper.Map<User>(userDto);

            var createUser = await _auth.Register(user, userDto.Password);

            var userReturn = _mapper.Map<UserDetailDto>(createUser);
            return CreatedAtRoute("GetUser", new { controller = "Users", id = createUser.Id }, userReturn);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto user)
        {
            var userData = await _auth.Login(user.Username, user.Password);
            if (userData == null)
                return Unauthorized();

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userData.Id + ""),
                new Claim(ClaimTypes.Name, userData.Username)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config.GetSection("AppSettings:Token").Value));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.Now.AddDays(1),
                SigningCredentials = creds
            };

            var tokenHandler = new JwtSecurityTokenHandler();

            var token = tokenHandler.CreateToken(tokenDescriptor);

            var userListDto = _mapper.Map<UserForListDto>(userData);

            return Ok(new
            {
                token = tokenHandler.WriteToken(token),
                user = userListDto
            });

        }
    }
}
