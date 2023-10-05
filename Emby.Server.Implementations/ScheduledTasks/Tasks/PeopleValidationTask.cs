#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CacheManager.Core.Logging;
using Emby.Server.Implementations.Library.Validators;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.ScheduledTasks.Tasks
{
    /// <summary>
    /// Class PeopleValidationTask.
    /// </summary>
    public class PeopleValidationTask : IScheduledTask
    {
        private readonly IFileSystem _fileSystem;

        /// <summary>
        /// The library manager.
        /// </summary>
        private readonly ILocalizationManager _localization;
        private readonly ILogger<PeopleValidationTask> _logger;
        private readonly IItemService _itemService;
        private readonly IItemQueryService _itemQueryService;
        private readonly IServerConfigurationManager _serverConfigurationManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="PeopleValidationTask" /> class.
        /// </summary>
        /// <param name="fileSystem">The file system.</param>
        /// <param name="localization">The localization manager.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="itemService">The item service.</param>
        /// <param name="itemQueryService">The item query service.</param>
        /// <param name="serverConfigurationManager">The server configuration.</param>
        public PeopleValidationTask(
            IFileSystem fileSystem,
            ILocalizationManager localization,
            ILogger<PeopleValidationTask> logger,
            IItemService itemService,
            IItemQueryService itemQueryService,
            IServerConfigurationManager serverConfigurationManager)
        {
            _fileSystem = fileSystem;
            _localization = localization;
            _logger = logger;
            _itemService = itemService;
            _itemQueryService = itemQueryService;
            _serverConfigurationManager = serverConfigurationManager;
        }

        public string Name => _localization.GetLocalizedString("TaskRefreshPeople");

        public string Description => _localization.GetLocalizedString("TaskRefreshPeopleDescription");

        public string Category => _localization.GetLocalizedString("TasksLibraryCategory");

        public string Key => "RefreshPeople";

        public bool IsHidden => false;

        public bool IsEnabled => true;

        public bool IsLogged => true;

        /// <summary>
        /// Creates the triggers that define when the task will run.
        /// </summary>
        /// <returns>An <see cref="IEnumerable{TaskTriggerInfo}"/> containing the default trigger infos for this task.</returns>
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerInterval,
                    IntervalTicks = TimeSpan.FromDays(7).Ticks
                }
            };
        }

        /// <inheritdoc />
        public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
                        // Ensure the location is available.
            Directory.CreateDirectory(_serverConfigurationManager.ApplicationPaths.PeoplePath);

            return new PeopleValidator(_logger, _fileSystem, _itemService, _itemQueryService).ValidatePeople(cancellationToken, progress);
        }
    }
}
