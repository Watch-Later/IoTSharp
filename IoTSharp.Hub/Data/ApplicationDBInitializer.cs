﻿using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using static IoTSharp.Hub.Controllers.InstallerController;

namespace IoTSharp.Hub.Data
{
    public class ApplicationDBInitializer
    {

        private readonly RoleManager<IdentityRole> _role;

        private ApplicationDbContext _context;
        private ILogger _logger;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly SignInManager<IdentityUser> _signInManager;
        public ApplicationDBInitializer(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            IConfiguration configuration, ILogger<ApplicationDBInitializer> logger,
            ApplicationDbContext context, RoleManager<IdentityRole> role
            )
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _logger = logger;
            _context = context;
            _role = role;
        }
        public void SeedRole()
        {
            var roles = new IdentityRole[]{new IdentityRole(nameof(UserRole.Anonymous))
                    , new IdentityRole(nameof(UserRole.NormalUser))
                    , new IdentityRole(nameof(UserRole.CustomerAdmin))
                    , new IdentityRole(nameof(UserRole.TenantAdmin))
                    , new IdentityRole(nameof(UserRole.SystemAdmin)) };
            roles.ToList().ForEach(role =>
            {
                _role.CreateAsync(role);
                _role.UpdateNormalizedRoleNameAsync(role);
            });
            _context.SaveChanges();
        }
        public void SeedUser(InstallDto model)
        {
            var tenant = _context.Tenant.FirstOrDefault(t => t.EMail == model.TenantEMail);
            var customer = _context.Customer.FirstOrDefault(t => t.Email == model.CustomerEMail);
            if (tenant == null && customer == null)
            {
                tenant = new Tenant() { Id = Guid.NewGuid(), Name = model.TenantName, EMail = model.TenantEMail };
                customer = new Customer() { Id = Guid.NewGuid(), Name = model.CustomerName, Email = model.CustomerEMail };
                customer.Tenant = tenant;
                tenant.Customers = new List<Customer>();
                tenant.Customers.Add(customer);
                _context.Tenant.Add(tenant);
                _context.Customer.Add(customer);
                _context.SaveChanges();
            }
            IdentityUser user = _userManager.FindByEmailAsync(model.Email).GetAwaiter().GetResult();
            if (user == null)
            {
                user = new IdentityUser
                {
                    Email = model.Email,
                    UserName = model.Email,
                    PhoneNumber = model.PhoneNumber
                };
                var result = _userManager.CreateAsync(user, model.Password).GetAwaiter().GetResult();
                if (result.Succeeded)
                {
                    _signInManager.SignInAsync(user, false);
                    _signInManager.UserManager.AddClaimAsync(user, new Claim(ClaimTypes.Email, model.Email));
                    _signInManager.UserManager.AddClaimAsync(user, new Claim(IoTSharpClaimTypes.Customer, customer.Id.ToString()));
                    _signInManager.UserManager.AddClaimAsync(user, new Claim(IoTSharpClaimTypes.Tenant, tenant.Id.ToString()));
                    _signInManager.UserManager.AddToRolesAsync(user, new[] {
                                        nameof(UserRole.Anonymous),
                                        nameof(UserRole.NormalUser),
                                        nameof(UserRole.CustomerAdmin),
                                        nameof(UserRole.TenantAdmin),
                                        nameof(UserRole.SystemAdmin)});
                }
            }
            var rship = new Relationship
            {
                IdentityUser = _context.Users.Find(user.Id),
                Customer = _context.Customer.Find(customer.Id),
                Tenant = _context.Tenant.Find(tenant.Id)
            };
            _context.Add(rship);
            _context.SaveChanges();
        }
    }
}
