﻿using System;
using System.Threading.Tasks;
using System.Linq;
using AgileConfig.Server.Apisite.Filters;
using AgileConfig.Server.Apisite.Models;
using AgileConfig.Server.Data.Entity;
using AgileConfig.Server.IService;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using AgileConfig.Server.Common;
using System.Text;
using System.Dynamic;
using AgileConfig.Server.Apisite.Utilites;

namespace AgileConfig.Server.Apisite.Controllers
{
    [Authorize]
    [ModelVaildate]
    public class ConfigController : Controller
    {
        private readonly IConfigService _configService;
        private readonly IModifyLogService _modifyLogService;
        private readonly IRemoteServerNodeProxy _remoteServerNodeProxy;
        private readonly IServerNodeService _serverNodeService;
        private readonly IAppService _appService;

        public ConfigController(
                                IConfigService configService,
                                IModifyLogService modifyLogService,
                                IRemoteServerNodeProxy remoteServerNodeProxy,
                                IServerNodeService serverNodeService,
                                 IAppService appService)
        {
            _configService = configService;
            _modifyLogService = modifyLogService;
            _remoteServerNodeProxy = remoteServerNodeProxy;
            _serverNodeService = serverNodeService;
            _appService = appService;
        }

        [TypeFilter(typeof(PremissionCheckAttribute), Arguments = new object[] { "Config.Add", Functions.Config_Add })]
        [HttpPost]
        public async Task<IActionResult> Add([FromBody] ConfigVM model)
        {
            if (model == null)
            {
                throw new ArgumentNullException("model");
            }

            var app = await _appService.GetAsync(model.AppId);
            if (app == null)
            {
                return Json(new
                {
                    success = false,
                    message = $"应用（{model.AppId}）不存在。"
                });
            }

            var oldConfig = await _configService.GetByAppIdKey(model.AppId, model.Group, model.Key);
            if (oldConfig != null)
            {

                return Json(new
                {
                    success = false,
                    message = "配置已存在，请更改输入的信息。"
                });
            }

            var config = new Config();
            config.Id = string.IsNullOrEmpty(config.Id) ? Guid.NewGuid().ToString("N") : config.Id;
            config.Key = model.Key;
            config.AppId = model.AppId;
            config.Description = model.Description;
            config.Value = model.Value;
            config.Group = model.Group;
            config.Status = ConfigStatus.Enabled;
            config.CreateTime = DateTime.Now;
            config.UpdateTime = null;
            config.OnlineStatus = OnlineStatus.WaitPublish;
            config.EditStatus = EditStatus.Add;

            var result = await _configService.AddAsync(config);

            if (result)
            {
                dynamic param = new ExpandoObject();
                param.config = config;
                param.userName = this.GetCurrentUserName();
                TinyEventBus.Instance.Fire(EventKeys.ADD_CONFIG_SUCCESS, param);
            }

            return Json(new
            {
                success = result,
                message = !result ? "新建配置失败，请查看错误日志" : "",
                data = config
            });
        }

        [TypeFilter(typeof(PremissionCheckAttribute), Arguments = new object[] { "Config.AddRange", Functions.Config_Add })]
        [HttpPost]
        public async Task<IActionResult> AddRange([FromBody] List<ConfigVM> model)
        {
            if (model == null || model.Count == 0)
            {
                throw new ArgumentNullException("model");
            }

            var configs = await _configService.GetByAppIdAsync(model.First().AppId);

            var oldDict = new Dictionary<string, string>();
            configs.ForEach(item =>
            {
                var newkey = item.Key;
                if (!string.IsNullOrEmpty(item.Group))
                {
                    newkey = $"{item.Group}:{item.Key}";
                }
                oldDict.Add(newkey, item.Value);
            });

            var addConfigs = new List<Config>();
            //judge if json key already in configs
            foreach (var item in model)
            {
                var newkey = item.Key;
                if (!string.IsNullOrEmpty(item.Group))
                {
                    newkey = $"{item.Group}:{item.Key}";
                }
                if (oldDict.ContainsKey(newkey))
                {
                    return Json(new
                    {
                        success = false,
                        message = "存在重复的配置：" + item.Key
                    });
                }

                var config = new Config();
                config.Id = Guid.NewGuid().ToString("N");
                config.Key = item.Key;
                config.AppId = item.AppId;
                config.Description = item.Description;
                config.Value = item.Value;
                config.Group = item.Group;
                config.Status = ConfigStatus.Enabled;
                config.CreateTime = DateTime.Now;
                config.UpdateTime = null;
                config.OnlineStatus = OnlineStatus.WaitPublish;
                config.EditStatus = EditStatus.Add;

                addConfigs.Add(config);
            }

            var result = await _configService.AddRangeAsync(addConfigs);

            if (result)
            {
                var userName = this.GetCurrentUserName();
                addConfigs.ForEach(c =>
                {
                    dynamic param = new ExpandoObject();
                    param.config = c;
                    param.userName = userName;
                    TinyEventBus.Instance.Fire(EventKeys.ADD_CONFIG_SUCCESS, param);
                });
            }

            return Json(new
            {
                success = result,
                message = !result ? "批量新增配置失败，请查看错误日志" : ""
            });
        }

        [TypeFilter(typeof(PremissionCheckAttribute), Arguments = new object[] { "Config.Edit", Functions.Config_Edit })]
        [HttpPost]
        public async Task<IActionResult> Edit([FromBody] ConfigVM model)
        {
            if (model == null)
            {
                throw new ArgumentNullException("model");
            }

            var config = await _configService.GetAsync(model.Id);
            if (config == null)
            {
                return Json(new
                {
                    success = false,
                    message = "未找到对应的配置项。"
                });
            }

            var app = await _configService.GetByAppIdAsync(model.AppId);
            if (!app.Any())
            {
                return Json(new
                {
                    success = false,
                    message = $"应用（{model.AppId}）不存在。"
                });
            }

            var oldConfig = new Config
            {
                Key = config.Key,
                Group = config.Group,
                Value = config.Value
            };
            if (config.Group != model.Group || config.Key != model.Key)
            {
                var anotherConfig = await _configService.GetByAppIdKey(model.AppId, model.Group, model.Key);
                if (anotherConfig != null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "配置键已存在，请重新输入。"
                    });
                }
            }

            config.AppId = model.AppId;
            config.Description = model.Description;
            config.Key = model.Key;
            config.Value = model.Value;
            config.Group = model.Group;
            config.UpdateTime = DateTime.Now;

            if (!IsOnlyUpdateDescription(config, oldConfig))
            {
                var isPublished = await _configService.IsPublishedAsync(config.Id);
                if (isPublished)
                {
                    //如果是已发布的配置，修改后状态设置为编辑
                    config.EditStatus = EditStatus.Edit;
                }
                else
                {
                    //如果没有发布，说明是新增的，一直维持新增状态
                    config.EditStatus = EditStatus.Add;
                }
                config.OnlineStatus = OnlineStatus.WaitPublish;
            }

            var result = await _configService.UpdateAsync(config);

            if (result)
            {
                dynamic param = new ExpandoObject();
                param.config = config;
                param.userName = this.GetCurrentUserName();
                param.oldConfig = config;
                TinyEventBus.Instance.Fire(EventKeys.EDIT_CONFIG_SUCCESS, param);
            }

            return Json(new
            {
                success = result,
                message = !result ? "修改配置失败，请查看错误日志。" : ""
            });
        }

        /// <summary>
        /// 是否只是修改了描述信息
        /// </summary>
        /// <param name="newConfig"></param>
        /// <param name="oldConfig"></param>
        /// <returns></returns>
        private bool IsOnlyUpdateDescription(Config newConfig, Config oldConfig)
        {
            return newConfig.Key == oldConfig.Key && newConfig.Group == oldConfig.Group && newConfig.Value == oldConfig.Value;
        }

        [HttpGet]
        public async Task<IActionResult> All()
        {
            var configs = await _configService.GetAllConfigsAsync();

            return Json(new
            {
                success = true,
                data = configs
            });
        }

        /// <summary>
        /// 按多条件进行搜索
        /// </summary>
        /// <param name="appId">应用id</param>
        /// <param name="group">分组</param>
        /// <param name="key">键</param>
        /// <param name="onlineStatus">在线状态</param>
        /// <param name="pageSize">分页大小</param>
        /// <param name="current">当前页</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> Search(string appId, string group, string key, OnlineStatus? onlineStatus, int pageSize = 20, int current = 1)
        {
            if (pageSize <= 0)
            {
                throw new ArgumentException("pageSize can not less then 1 .");
            }
            if (current <= 0)
            {
                throw new ArgumentException("pageIndex can not less then 1 .");
            }

            var configs = await _configService.Search(appId, group, key);
            configs = configs.Where(c => c.Status == ConfigStatus.Enabled).ToList();
            if (onlineStatus.HasValue)
            {
                configs = configs.Where(c => c.OnlineStatus == onlineStatus).ToList();
            }
            configs = configs.OrderBy(c => c.AppId).ThenBy(c=>c.EditStatus).ThenBy(c => c.Group).ThenBy(c => c.Key).ThenBy(c=>c.CreateTime).ToList();

            var page = configs.Skip((current - 1) * pageSize).Take(pageSize).ToList();
            var total = configs.Count();

            return Json(new
            {
                current,
                pageSize,
                success = true,
                total = total,
                data = page
            });
        }

        [HttpGet]
        public async Task<IActionResult> Get(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException("id");
            }

            var config = await _configService.GetAsync(id);

            return Json(new
            {
                success = config != null,
                data = config,
                message = config == null ? "未找到对应的配置项。" : ""
            });
        }

        [TypeFilter(typeof(PremissionCheckAttribute), Arguments = new object[] { "Config.Delete", Functions.Config_Delete })]
        [HttpPost]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException("id");
            }

            var config = await _configService.GetAsync(id);
            if (config == null)
            {
                return Json(new
                {
                    success = false,
                    message = "未找到对应的配置项。"
                });
            }

            config.EditStatus = EditStatus.Deleted;
            config.OnlineStatus = OnlineStatus.WaitPublish;

            var isPublished = await _configService.IsPublishedAsync(config.Id);
            if (!isPublished)
            {
                //如果已经没有发布过直接删掉
                config.Status = ConfigStatus.Deleted;
            }

            var result = await _configService.UpdateAsync(config);
            if (result)
            {
                dynamic param = new ExpandoObject();
                param.config = config;
                param.userName = this.GetCurrentUserName();
                TinyEventBus.Instance.Fire(EventKeys.DELETE_CONFIG_SUCCESS, param);
            }

            return Json(new
            {
                success = result,
                message = !result ? "删除配置失败，请查看错误日志" : ""
            });
        }


        [TypeFilter(typeof(PremissionCheckAttribute), Arguments = new object[] { "Config.Rollback", Functions.Config_Edit })]
        [HttpPost]
        public async Task<IActionResult> Rollback(string configId, string logId)
        {
            if (string.IsNullOrEmpty(configId))
            {
                throw new ArgumentNullException("configId");
            }
            if (string.IsNullOrEmpty(logId))
            {
                throw new ArgumentNullException("logId");
            }

            var config = await _configService.GetAsync(configId);
            if (config == null)
            {
                return Json(new
                {
                    success = false,
                    message = "未找到对应的配置项。"
                });
            }
            var oldConfig = new Config
            {
                Key = config.Key,
                Group = config.Group,
                Value = config.Value
            };

            var log = await _modifyLogService.GetAsync(logId);
            if (log == null)
            {
                return Json(new
                {
                    success = false,
                    message = "未找到对应的配置项的历史记录项。"
                });
            }
            config.Key = log.Key;
            config.Group = log.Group;
            config.Value = log.Value;
            config.UpdateTime = DateTime.Now;

            var result = await _configService.UpdateAsync(config);
            if (result)
            {
                dynamic param = new ExpandoObject();
                param.config = config;
                param.modifyLog = log;
                param.oldConfig = oldConfig;
                param.userName = this.GetCurrentUserName();
                TinyEventBus.Instance.Fire(EventKeys.ROLLBACK_CONFIG_SUCCESS, param);
            }

            return Json(new
            {
                success = result,
                message = !result ? "回滚失败，请查看错误日志。" : ""
            });
        }

        [HttpGet]
        public async Task<IActionResult> ModifyLogs(string configId)
        {
            if (string.IsNullOrEmpty(configId))
            {
                throw new ArgumentNullException("configId");
            }

            var logs = await _modifyLogService.Search(configId);

            return Json(new
            {
                success = true,
                data = logs.OrderByDescending(l => l.ModifyTime).ToList()
            }); ;
        }

        /// <summary>
        /// 发布所有待发布的配置项
        /// </summary>
        /// <param name="appId">应用id</param>
        /// <returns></returns>
        [TypeFilter(typeof(PremissionCheckAttribute), Arguments = new object[] { "Config.PublishAsync", Functions.Config_Publish })]
        [HttpPost]
        public async Task<IActionResult> Publish(string appId)
        {
            if (string.IsNullOrEmpty(appId))
            {
                throw new ArgumentNullException("appId");
            }

            var ret = _configService.Publish(appId, this.GetCurrentUserId());

            if (ret.result)
            {
                var timelineNode = await _configService.GetPublishTimeLineNode(ret.publishTimelineId);
                dynamic param = new ExpandoObject();
                param.publishTimelineNode = timelineNode;
                param.userName = this.GetCurrentUserName();
                TinyEventBus.Instance.Fire(EventKeys.PUBLISH_CONFIG_SUCCESS, param);
            }

            return Json(new
            {
                success = ret.result,
                message = !ret.result ? "上线配置失败，请查看错误日志" : ""
            });
        }

        /// <summary>
        /// 预览上传的json文件
        /// </summary>
        /// <returns></returns>
        public IActionResult PreViewJsonFile()
        {
            List<IFormFile> files = Request.Form.Files.ToList();
            if (!files.Any())
            {
                return Json(new
                {
                    success = false,
                    message = "请上传Json文件"
                });
            }

            var jsonFile = files.First();
            using (var stream = jsonFile.OpenReadStream())
            {
                var dict = JsonConfigurationFileParser.Parse(stream);

                var addConfigs = new List<Config>();
                //judge if json key already in configs
                foreach (var key in dict.Keys)
                {
                    var newKey = key;
                    var group = "";
                    var paths = key.Split(":");
                    if (paths.Length > 1)
                    {
                        //如果是复杂key，取最后一个为真正的key，其他作为group
                        newKey = paths[paths.Length - 1];
                        group = string.Join(":", paths.ToList().Take(paths.Length - 1));
                    }

                    var config = new Config();
                    config.Key = newKey;
                    config.Description = "";
                    config.Value = dict[key];
                    config.Group = group;
                    config.Id = Guid.NewGuid().ToString();
                    addConfigs.Add(config);
                }

                return Json(new
                {
                    success = true,
                    data = addConfigs
                });
            }
        }

        /// <summary>
        /// 导出json文件
        /// </summary>
        /// <param name="appId">应用id</param>
        /// <returns></returns>
        public async Task<IActionResult> ExportJson(string appId)
        {
            if (string.IsNullOrEmpty(appId))
            {
                throw new ArgumentNullException("appId");
            }

            var configs = await _configService.GetByAppIdAsync(appId);

            var dict = new Dictionary<string, string>();
            configs.ForEach(x =>
            {
                var key = _configService.GenerateKey(x);
                dict.Add(key, x.Value);
            });

            var json = DictionaryConvertToJson.ToJson(dict);

            return File(Encoding.UTF8.GetBytes(json), "application/json", $"{appId}.json");
        }

        /// <summary>
        /// 获取待发布的明细
        /// </summary>
        /// <param name="appId">应用id</param>
        /// <returns></returns>
        public async Task<IActionResult> WaitPublishStatus(string appId)
        {
            if (string.IsNullOrEmpty(appId))
            {
                throw new ArgumentNullException("appId");
            }

            var configs = await _configService.Search(appId, "", "");
            configs = configs.Where(x => x.Status == ConfigStatus.Enabled && x.EditStatus != EditStatus.Commit).ToList();

            var addCount = configs.Count(x => x.EditStatus == EditStatus.Add);
            var editCount = configs.Count(x => x.EditStatus == EditStatus.Edit);
            var deleteCount = configs.Count(x => x.EditStatus == EditStatus.Deleted);

            return Json(new
            {
                success = true,
                data = new 
                {
                    addCount,
                    editCount,
                    deleteCount
                }
            });
        }
    }
}
