﻿using AgileConfig.Server.Data.Abstraction;
using AgileConfig.Server.Data.Entity;
using AgileConfig.Server.Data.Freesql;

namespace AgileConfig.Server.Data.Repository.Freesql
{
    public class AppInheritancedRepository : FreesqlRepository<AppInheritanced, string>, IAppInheritancedRepository
    {
        private readonly IFreeSqlFactory freeSqlFactory;

        public AppInheritancedRepository(IFreeSqlFactory freeSqlFactory) : base(freeSqlFactory.Create())
        {
            this.freeSqlFactory = freeSqlFactory;
        }
    }
}
