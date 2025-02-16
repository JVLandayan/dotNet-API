﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Data.SqlClient;
using System.Data;
using EcoSystemAPI.Data;
using AutoMapper;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Runtime;
using Microsoft.AspNetCore.Authorization;
using EcoSystemAPI.Context;
using EcoSystemAPI.Context.Models;
using EcoSystemAPI.uow.Interfaces;
using EcoSystemAPI.Core.Dtos;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;
using System.Security.Cryptography;
using EcosystemAPI.util.services;

namespace EcoSystemAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]

    public class AccountsController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IAccountsRepo _repository;
        private readonly IMapper _mapper;
        private readonly IWebHostEnvironment _env;
        private IUserService _userService;

        public AccountsController(IAccountsRepo repository, IConfiguration configuration, IMapper mapper, IWebHostEnvironment env, IUserService userService)
        {
            _configuration = configuration;
            _repository = repository;
            _mapper = mapper;
            _env = env;
            _userService = userService;
        }

        //api/accounts
        [HttpGet]
        [Authorize]
        public ActionResult<IEnumerable<AccountsReadDto>> GetAllAccounts()
        {
            var accountItems = _repository.GetAllAccounts();
            return Ok(_mapper.Map<IEnumerable<AccountsReadDto>>(accountItems));
        }

        //api/accounts/{id}
        [HttpGet("{id}", Name="GetAccountsById")]
        [Authorize]
        public ActionResult<IEnumerable<AccountsReadDto>> GetAccountsById(int id)
        {
            var accountItem = _repository.GetAccountsById(id);
            if (accountItem != null)
            {
                return Ok(_mapper.Map<AccountsReadDto>(accountItem));
            }
            else
                return NotFound(new { message = "Account doesn't exist" });

        }

        [HttpPost]
        [Authorize]
        public ActionResult<AccountsReadDto> CreateAccount(AccountsCreateDto accountCreateDto)
        {
            
            var userExists = _repository.GetAllAccounts().Any(p => p.Email == accountCreateDto.Email);
            if (userExists)
            {
                return BadRequest(new { message = "Email is currently being used" });
            }

            var modifiedData = new AccountsCreateDto
            {
                FirstName = accountCreateDto.FirstName.ToUpper(),
                PhotoFileName = accountCreateDto.PhotoFileName,
                AuthId = 2,
                Email = accountCreateDto.Email.ToLower(),
                LastName = accountCreateDto.LastName.ToUpper(),
                MiddleName = accountCreateDto.MiddleName.ToUpper(),
                Password = _userService.HashPassword("123"),
                ResetToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(DateTime.Now.ToString("yyyyMMddHHmmssfff")))
             };


                var accountModel = _mapper.Map<Account>(modifiedData);
                _repository.CreateAccount(accountModel);
                _repository.SaveChanges();

                var accountsReadDto = _mapper.Map<AccountsReadDto>(accountModel);
                return CreatedAtRoute(nameof(GetAccountsById), new { Id = accountsReadDto.Id }, accountsReadDto);

            

        }
        // GET api/accounts/author/
        [HttpGet("author/{id}")]
        public ActionResult<IEnumerable<AccountsAuthorRead>> GetAuthorById(int id)
        {
            var accountItem = _repository.GetAccountsById(id);
            if (accountItem == null)
            {
                return NotFound(new { message = "Account doesn't exist" });
            }
            if (accountItem != null)
            {
                return Ok(_mapper.Map<AccountsAuthorRead>(accountItem));
            }
            else
                return NotFound();

        }



        [HttpPatch("{id}")]
        [Authorize]

        public ActionResult PartialAccountUpdate(int id, JsonPatchDocument<AccountsUpdateDto> patchDoc) 
        {
            var accountModelFromRepo = _repository.GetAccountsById(id);
            if (accountModelFromRepo == null)
            {
                return NotFound();
            }

            var accountToPatch = _mapper.Map<AccountsUpdateDto>(accountModelFromRepo);


            if(!TryValidateModel(accountToPatch))
            {
                return ValidationProblem();
            }
            _mapper.Map(accountToPatch, accountModelFromRepo);
            _repository.UpdateAccount(accountModelFromRepo);
            _repository.SaveChanges();
            return NoContent();

        }



        [HttpPut("{id}/image")]
        [Authorize]

        public ActionResult UpdateAccount(int id, AccountsUpdateDto accountsUpdateDto)
        {
            var accountModelFromRepo = _repository.GetAccountsById(id);
            if (accountModelFromRepo == null)
            {
                return NotFound();
            }
            var modifiedData = new AccountsUpdateDto
            {
                Email = accountModelFromRepo.Email,
                FirstName = accountModelFromRepo.FirstName,
                LastName = accountModelFromRepo.LastName,
                MiddleName = accountModelFromRepo.MiddleName,
                Password = accountModelFromRepo.Password,
                PhotoFileName = accountsUpdateDto.PhotoFileName,
                ResetToken = accountModelFromRepo.ResetToken
            };



            _mapper.Map(modifiedData, accountModelFromRepo);

            _repository.UpdateAccount(accountModelFromRepo);

            _repository.SaveChanges();

            return NoContent();
        }

        [HttpPut("{id}/pass")]
        [Authorize]

        public ActionResult UpdatePassword(int id, AccountsUpdateDto accountsUpdateDto)
        {
            var accountModelFromRepo = _repository.GetAccountsById(id);
            if (accountModelFromRepo == null)
            {
                return NotFound();
            }

            var modifiedData = new AccountsUpdateDto
            {
                Email = accountModelFromRepo.Email,
                FirstName = accountModelFromRepo.FirstName,
                LastName = accountModelFromRepo.LastName,
                MiddleName = accountModelFromRepo.MiddleName,
                Password = _userService.HashPassword(accountsUpdateDto.Password),
                PhotoFileName = accountModelFromRepo.PhotoFileName,
                ResetToken = accountModelFromRepo.ResetToken
            };



            _mapper.Map(modifiedData, accountModelFromRepo);

            _repository.UpdateAccount(accountModelFromRepo);

            _repository.SaveChanges();

            return NoContent();
        }


        [HttpDelete("{id}")]
        [Authorize]
        public ActionResult DeleteAccount(int id)
        {
            var photoFolderPath = _env.ContentRootPath + "/Photos/";
            var accountModelFromRepo = _repository.GetAccountsById(id);
            if (accountModelFromRepo== null)
            {
                return NotFound();
            }

            _repository.DeleteAccount(accountModelFromRepo);
            System.IO.File.Delete(photoFolderPath + accountModelFromRepo.PhotoFileName);
            _repository.SaveChanges();
            return NoContent();



        }
        [Route("SaveFile")]
        [HttpPost]
        [Authorize]
        public JsonResult SaveFile()
        {
            try
            {
                var httpRequest = Request.Form;
                var postedFile = httpRequest.Files[0];
                string filename = postedFile.FileName;
                var physicalPath = _env.ContentRootPath + "/Photos/" + filename;
                var filextn = Path.GetExtension(physicalPath); //Extension
                var newFileName = DateTime.Now.ToString("yyyyMMddHHmmssfff") + filextn;
                var newFilePath = _env.ContentRootPath + "/Photos/"+ newFileName;

                using (var stream = new FileStream(newFilePath, FileMode.Create))
                {
                    postedFile.CopyTo(stream);
                }

                return new JsonResult(newFileName);
            }
            catch (Exception)
            {
                return new JsonResult("Anonymous.png");
            }
        }

    }

}
 