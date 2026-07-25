using Microsoft.EntityFrameworkCore;
using meesuanruam_service.DTO.table;

namespace meesuanruam_service.DTO
{
    public class meeDB : DbContext
    {
        public meeDB(DbContextOptions<meeDB> options)
       : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

        }

        public DbSet<REPORT> report { get; set; }
        public DbSet<REPORT_TOPIC> report_topic { get; set; }
        public DbSet<FILE> file { get; set; }
        public DbSet<COMMENT> comment { get; set; }
        public DbSet<USER> user { get; set; }
        public DbSet<PROJECT> project { get; set; }
        public DbSet<MEASURES> measures { get; set; }
        public DbSet<PROCESS> process { get; set; }
        public DbSet<INDICATORS_ACTHIEVEMENT> indicators_acthievement { get; set; }
    }
}
