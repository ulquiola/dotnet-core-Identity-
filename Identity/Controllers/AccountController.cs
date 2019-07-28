using System.Threading.Tasks;
using Identity.Models;
using Identity.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Controllers
{
    public class AccountController: Controller
    {
        private readonly SignInManager<User> _signInManager;
        private readonly UserManager<User> _userManager;
        public AccountController(SignInManager<User> signInManager,UserManager<User> userManager)
        {
            _signInManager=signInManager;
            _userManager=userManager;
        }
        public  IActionResult Index()
        {
            return Content("Welcome to Account");
        }
        public IActionResult Register()
        {
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel registerViewModel)
        {
            if(ModelState.IsValid)
            {
                var user=new User(){
                    UserName=registerViewModel.UserName
                };
                var result= await _userManager.CreateAsync(user,registerViewModel.Password);
                if(result.Succeeded)
                {
                    return RedirectToAction("Index","Home");
                }
            }
            return View(registerViewModel);            
        }

        public IActionResult Login()
        {
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel loginViewModel)
        {
            if(ModelState.IsValid)
            {
                var user= await _userManager.FindByNameAsync(loginViewModel.UserName);
                if(user!=null)
                {
                    var result=await _signInManager.PasswordSignInAsync(user,loginViewModel.Password,false,false);
                    if(result.Succeeded)
                    {
                        return RedirectToAction("Index","Home");
                    }
                    else{
                        ModelState.AddModelError("", "密码不正确");
                    }
                }
                else
                {
                    ModelState.AddModelError("", "用户不存在");
                }
            }
            return View(loginViewModel);
        }
    }
}