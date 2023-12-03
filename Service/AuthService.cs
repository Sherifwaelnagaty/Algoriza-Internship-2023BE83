﻿using Algoriza_Project_2023BE83.Helpers;
using Algoriza_Project_2023BE83.Models;
using Core.Domain;
using Core.Service;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Service
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<Users> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly JWT _jwt;
        public AuthService (UserManager<Users> userManager,IOptions<JWT>jwt, RoleManager<IdentityRole> roleManager) 
        { 
            _userManager = userManager;
            _jwt = jwt.Value;
            _roleManager = roleManager;
        }

        public async Task<string> AddRoleAsync(AddRoleModel model)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null|| !await _roleManager.RoleExistsAsync(model.Role))
            {
                return "Invalid user ID or Role";
            }
            if(await _userManager.IsInRoleAsync(user, model.Role))
            {
                return "User already assigned to this role";
            }
            var result = await _userManager.AddToRoleAsync(user, model.Role);

            if (result.Succeeded)
            {
                return string.Empty;
            }
            return "Something went wrong";
        }

        public async Task<Auth> LoginAsync(Login model)
        {
            var auth = new Auth();
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null || !await _userManager.CheckPasswordAsync(user, model.Password))
            {
                auth.Message = "Email or Password is incorrect";
                return auth;
            }
            var jwtSecurityToken = await CreateJwtToken(user);
            var rolesList = await _userManager.GetRolesAsync(user);
            auth.IsAuthenticated = true;
            auth.Token = new JwtSecurityTokenHandler().WriteToken(jwtSecurityToken);
            auth.Email = user.Email;
            auth.UserName = user.UserName;
            auth.ExpiresOn = jwtSecurityToken.ValidTo;
            auth.Roles = rolesList.ToList();
            return auth;
        }

        public async Task<Auth> RegisterAsync(Register model)
        {
            if (await _userManager.FindByEmailAsync(model.Email) is not null) 
            {
                return new Auth { Message = "Email is already registered!" };
            }
            if (await _userManager.FindByNameAsync(model.Username) is not null)
            {
                return new Auth { Message = "Username is already registered!" };
            }
            var user = new Users
            {
                UserName = model.Username,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,                
            };
            var result = await _userManager.CreateAsync(user,model.Password);
            if(!result.Succeeded)
            {
                var errors = string.Empty;
                foreach (var error in result.Errors)
                {
                    errors += $"{error.Description},";
                }
                return new Auth { Message = errors };
            }
            await _userManager.AddToRoleAsync(user, "User");
            
            var jwtSecurityToken = await CreateJwtToken(user);
            return new Auth
            {
                Email = user.Email,
                ExpiresOn =jwtSecurityToken.ValidTo,
                IsAuthenticated = true,
                Roles = new List<string> { "User" },
                Token = new JwtSecurityTokenHandler().WriteToken(jwtSecurityToken),
                UserName = user.UserName,
            };
        }
        private async Task<JwtSecurityToken> CreateJwtToken(Users user)
        {
            var userClaims = await _userManager.GetClaimsAsync(user);
            var roles = await _userManager.GetRolesAsync(user);
            var roleClaims = new List<Claim>();

            foreach (var role in roles)
                roleClaims.Add(new Claim("roles", role));

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("uid", user.Id)
            }
            .Union(userClaims)
            .Union(roleClaims);

            var symmetricSecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
            var signingCredentials = new SigningCredentials(symmetricSecurityKey, SecurityAlgorithms.HmacSha256);

            var jwtSecurityToken = new JwtSecurityToken(
            issuer: _jwt.Issuer,
                audience: _jwt.Audience,
                claims: claims,
                expires: DateTime.Now.AddDays(_jwt.DurationInDays),
                signingCredentials: signingCredentials);

            return jwtSecurityToken;
        }
    }

}
}
