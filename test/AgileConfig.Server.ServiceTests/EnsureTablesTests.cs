﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using AgileConfig.Server.Data.Freesql;
using System;
using System.Collections.Generic;
using System.Text;
using FreeSql;
using AgileConfig.Server.Data.Entity;

namespace AgileConfig.Server.ServiceTests
{
    public class EnsureTablesTests
    {
        [TestMethod()]
        public void ExistTableSqliteTest()
        {
            //sqlite
            string conn = "Data Source=agile_config.db";
            var sqllite_fsq = new FreeSqlBuilder()
                          .UseConnectionString(DataType.Sqlite, conn)
                          .Build();
            FluentApi.Config(sqllite_fsq);
            sqllite_fsq.Ado.ExecuteNonQuery("drop table agc_app");
            var ex = EnsureTables.ExistTable(sqllite_fsq);
            Assert.IsFalse(ex);
            sqllite_fsq.CodeFirst.SyncStructure<App>();
            ex = EnsureTables.ExistTable(sqllite_fsq);
            Assert.IsTrue(ex);
        }

        [TestMethod()]
        public void ExistTableSqlServerTest()
        {
            //SqlServer
            string conn = "TrustServerCertificate=True;Persist Security Info = False; User ID =dev; Password =dev; Initial Catalog =agile_config_test; Server =.";
            var sqlserver_fsq = new FreeSqlBuilder()
                          .UseConnectionString(DataType.SqlServer, conn)
                          .Build();
            FluentApi.Config(sqlserver_fsq);
            sqlserver_fsq.Ado.ExecuteNonQuery("drop table agc_app");
            var ex = EnsureTables.ExistTable(sqlserver_fsq);
            Assert.IsFalse(ex);
            sqlserver_fsq.CodeFirst.SyncStructure<App>();
            ex = EnsureTables.ExistTable(sqlserver_fsq);
            Assert.IsTrue(ex);
        }

        [TestMethod()]
        public void ExistTableMysqlTest()
        {
            //SqlServer
            string conn = "Database=agile_config_test;Data Source=192.168.0.125;User Id=root;Password=x;port=13306";
            var mysql_fsq = new FreeSqlBuilder()
                          .UseConnectionString(DataType.MySql, conn)
                          .Build();
            FluentApi.Config(mysql_fsq);
            try
            {
                mysql_fsq.Ado.ExecuteNonQuery("drop table agc_app");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            var ex = EnsureTables.ExistTable(mysql_fsq);
            Assert.IsFalse(ex);
            mysql_fsq.CodeFirst.SyncStructure<App>();
            ex = EnsureTables.ExistTable(mysql_fsq);
            Assert.IsTrue(ex);
        }

        [TestMethod()]
        public void ExistTableOracleTest()
        {
            //SqlServer
            string conn = "user id=CLINIC;password=CLINIC;data source=192.168.0.91/orcl";
            var oracle_fsq = new FreeSqlBuilder()
                          .UseConnectionString(DataType.Oracle, conn)
                          .Build();
            FluentApi.Config(oracle_fsq);
            oracle_fsq.Ado.ExecuteNonQuery("drop table \"agc_app\" ");
            var ex = EnsureTables.ExistTable(oracle_fsq);
            Assert.IsFalse(ex);
            oracle_fsq.CodeFirst.SyncStructure<App>();
            ex = EnsureTables.ExistTable(oracle_fsq);
            Assert.IsTrue(ex);
        }

        [TestMethod()]
        public void ExistTablePostgreSqlTest()
        {
            //SqlServer
            string conn = "Host=192.168.0.125;Port=15432;Database=agileconfig;Username=postgres;Password=123456";
            var postgresql_fsq = new FreeSqlBuilder()
                          .UseConnectionString(DataType.PostgreSQL, conn)
                          .Build();
            FluentApi.Config(postgresql_fsq);
            postgresql_fsq.Ado.ExecuteNonQuery("drop table \"agc_app\" ");
            var ex = EnsureTables.ExistTable(postgresql_fsq);
            Assert.IsFalse(ex);
            postgresql_fsq.CodeFirst.SyncStructure<App>();
            ex = EnsureTables.ExistTable(postgresql_fsq);
            Assert.IsTrue(ex);
        }
    }
}