using Identity.Models;
using Microsoft.EntityFrameworkCore;

namespace Identity
{
    public class AppcationDbContext:DbContext
    {
        public AppcationDbContext(DbContextOptions options)
            :base(options)
        {
           
        }
        public DbSet<User> Users{get;set;}

    }
}