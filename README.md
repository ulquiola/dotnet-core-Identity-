#UserName设置中文

ASP.NET Core内置的标识框架在增加用户时包含了一些验证规则，其中就有对用户名进行验证的规则，这个规则是用来验证用户名所包含的字符是否符合设定，默认的字符集是“abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+”，这个字符集可以在Startup的ConfigureServices方法中配置，例如我们要求用户名只能包含数字，则可以在ConfigureServices方法中添加如下代码：
services.Configure<IdentityOptions>(options =>
{
    options.User.AllowedUserNameCharacters = "0123456789";
});

从这里我们可以看出，只要设定AllowedUserNameCharacters的值包含能够出现在用户名中的字符就可以了。但这也带来一个问题，如果我们希望用户名中包含中文，我们应该怎么做呢？
显然不可能把所有汉字全加到AllowedUserNameCharacters中去，通过反编译源代码（后来我在github上找到了源代码，不用去反编译了……地址：https://github.com/aspnet/Identity/tree/master/src/Core）可以看到UserManager在创建用户的方法CreateAsync方法中调用了UserValidator中的方法ValidateUserName，而验证用户名的业务逻辑就是在ValidateUserName方法中实现的。
题外话，通过查看源码，发现如果把AllowedUserNameCharacters设置为空字符串，用户名就可以包含任何字符。
UserValidator是接口IUserValidator的实现类，熟悉接口编程的我们应该马上就想到可以写一个我们自己的IUserValidator的实现类来替代它。加上ASP.NET Core内置依赖注入，用我们自定义的IUserValidator实现类来验证用户名应该完全没有问题。
我们在项目下添加一个文件夹Custom，在文件夹下添加一个类MyUserValidator，让它实现IUserValidator接口，下面是完整代码：
public class MyUserValidator<TUser> : IUserValidator<TUser> where TUser : class
{
    const string chinese = "{中}";

    public IdentityErrorDescriber Describer { get; private set; }
    public MyUserValidator(IdentityErrorDescriber errors = null)
    {
        Describer = errors ?? new IdentityErrorDescriber();
    }
    public async Task<IdentityResult> ValidateAsync(UserManager<TUser> manager, TUser user)
    {
        if (manager == null)
        {
            throw new ArgumentNullException(nameof(manager));
        }
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }
        var errors = new List<IdentityError>();
        await ValidateUserName(manager, user, errors);
        return errors.Count > 0 ? IdentityResult.Failed(errors.ToArray()) : IdentityResult.Success;
    }

    private async Task ValidateUserName(UserManager<TUser> manager, TUser user, ICollection<IdentityError> errors)
    {
        var userName = await manager.GetUserNameAsync(user);
        if (string.IsNullOrWhiteSpace(userName))
        {
            errors.Add(Describer.InvalidUserName(userName));
        }
        var characters = manager.Options.User.AllowedUserNameCharacters;
        bool allowChinese = false;
        if (characters.Contains(chinese))
        {
            allowChinese = true;
            characters = characters.Remove(characters.IndexOf(chinese), chinese.Length);
        }
        if (ContainsChinese(userName) && !allowChinese)
        {
            errors.Add(Describer.InvalidUserName(userName));
        }
        var tempName = RemoveChinese(userName);
        if (!string.IsNullOrEmpty(characters) && tempName.Any(c => !characters.Contains(c)))
        {
            errors.Add(Describer.InvalidUserName(userName));
        }
        var owner = await manager.FindByNameAsync(userName);
        if (owner != null &&
            !string.Equals(await manager.GetUserIdAsync(owner), await manager.GetUserIdAsync(user)))
        {
            errors.Add(Describer.DuplicateUserName(userName));
        }
    }

    //判断字符串是否包含汉字
    private bool ContainsChinese(string text)
    {
        return text.Any(c => c >= 0x4e00 && c <= 0x9fbb);
    }
    
    //移除字符串中的汉字
    private string RemoveChinese(string text)
    {
        StringBuilder sb = new StringBuilder();
        foreach(char c in text)
        {
            if(c>=0x4e00 && c <= 0x9fbb)
            {
                continue;
            }
            sb.Append(c);
        }
        return sb.ToString();
    }
}

在我们的验证规则中，只要AllowedUserNameCharacters中包含“{中}"，就表示允许用户名包含中文字符，其它代码比较简单，这里不再解释了。
要添加标识框架，我们已经在Startup的ConfigureServices中添加了以下代码：
            services.AddIdentity<User, Role>()
                .AddRoles<Role>()
                .AddEntityFrameworkStores<LedContext>();

注意这里的User和Role类是我自定义的用户和角色类，可以用标识框架提供的IdentityUser和IdentityRole代替它们。AddIdentity方法返回的是一个IdentityBuilder对象，这个对象是配置Identity服务的Builder类对象，查看这个对象的帮助文档，会发现里面有诸多Add开始的方法，用来添加各种服务，其中就有一个AddUserValidator方法，很显示，这个就是我们要找的方法。
把前面的代码改一下：
            services.AddIdentity<User, Role>()
                //添加自定义的用户验证器
                .AddUserValidator<MyUserValidator<User>>()
                .AddRoles<Role>()
                .AddEntityFrameworkStores<LedContext>();

为了方便起见，我们用代码添加一个用户。添加一个SeedData类，添加以下代码：
public static async Task Initialize(IServiceProvider serviceProvider)
{
      using (var context = new LedContext(
                serviceProvider.GetRequiredService<DbContextOptions<LedContext>>()))
      {
          if (!context.Users.Any())
          {
               await AddUser(serviceProvider);
          }
      }
}
private static async Task AddUser(IServiceProvider serviceProvider)
{
    var userManager = serviceProvider.GetService<UserManager<User>>();
    var user = new User { Id = "admin", UserName = "管理员" };
    try
    {
        var result = await userManager.CreateAsync(user);
    }
    catch(Exception ex)
    {
        Console.WriteLine(ex);
    }
}

修改Program类的Main方法：
        public static void Main(string[] args)
        {
            var host = CreateWebHostBuilder(args).Build();
            using (var scope = host.Services.CreateScope())
            {
                var service = scope.ServiceProvider;
                SeedData.Initialize(service).Wait();
            }
            host.Run();
        }

将AllowedUserNameCharacters设置为“abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789{中}”表示允许用户名包含字母数字和中文，在MyUserValidator的ValidateAsync方法中设置一个断点，然后按F5运行程序，程序会停留到我们的断点上，说明我们自己的验证规则起作用了。
但是查看数据库，你会发现用户并没有被创建，我们在SeedData的AddUser方法中的这行代码var result = await userManager.CreateAsync(user);设置一个断点，按F5运行程序，查看result的值，会发现仍然包含一个验证错误：InvalidUserName，这和我们没有添加自定义验证规则之前一样。
回头再看看UserManager的CreateAsync方法，发现这个方法调用的用来验证用户的ValidateUserAsync方法中有如下代码：
            foreach (var v in UserValidators)
            {
                var result = await v.ValidateAsync(this, user);
                if (!result.Succeeded)
                {
                    errors.AddRange(result.Errors);
                }
            }

从代码中可以看出，UserManager调用的验证器不止一个，将每个验证器返回的errors（验证错误集合）合并到一起，最后检查合并后的errors中是否包含验证错误项，如果有，验证失败，否则验证成功。
我们虽然添加了自定义的验证规则，但是标识框架默认的验证规则并没有被移除，因此验证的最终结果仍然是失败的。除非我们能够移除默认的验证规则。
Startup类的ConfigureServices方法的参数services是一个集合，我们可以在这个集合中找到默认的用户验证服务并移除它，这个验证服务是在添加了标识之后自动添加的，所以我们需要修改代码来移除它，修改ConfigureServices方法：
            services.AddIdentity<User, Role>()
                .AddUserValidator<MyUserValidator<User>>()
                .AddRoles<Role>()
                .AddEntityFrameworkStores<LedContext>();
            //移除默认的用户验证服务，以下代码必须添加到AddIdentity方法之后
            var service = services.FirstOrDefault(s => s.ImplementationType == typeof(UserValidator<User>));
            if (service != null)
            {
                services.Remove(service);
            }

作者：firechun
链接：https://www.jianshu.com/p/5f85aab34407
来源：简书
简书著作权归作者所有，任何形式的转载都请联系作者获得授权并注明出处。
