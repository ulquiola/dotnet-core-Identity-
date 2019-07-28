using System.ComponentModel.DataAnnotations;

namespace Identity.ViewModels
{
    public class RegisterViewModel
    {
        [Display(Name="用户名")]
        public string UserName { get; set; }
        [Display(Name="密码")]
        [DataType(DataType.Password)]
        public string Password { get; set; }
        [Display(Name="再次输入密码")]
        [DataType(DataType.Password)]
        [Compare("Password",ErrorMessage="两次密码不一致")]
        public string Confirmpassword { get; set; }  
    }
}