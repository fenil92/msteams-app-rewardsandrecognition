// <copyright file="RewardCycleStorageProvider.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace Microsoft.Teams.Apps.RewardAndRecognition.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Microsoft.Teams.Apps.RewardAndRecognition.Models;
    using Microsoft.WindowsAzure.Storage.Table;

    /// <summary>
    /// Reward cycle storage provider class.
    /// </summary>
    public class RewardCycleStorageProvider : StorageBaseProvider, IRewardCycleStorageProvider
    {
        private const string RewardCycleTable = "RewardCycleDetail";

        /// <summary>
        /// Sends logs to the Application Insights service.
        /// </summary>
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RewardCycleStorageProvider"/> class.
        /// </summary>
        /// <param name="storageOptions">A set of key/value application storage configuration properties.</param>
        /// <param name="logger">Instance to send logs to the application insights service.</param>
        public RewardCycleStorageProvider(IOptionsMonitor<StorageOptions> storageOptions, ILogger<RewardCycleStorageProvider> logger)
            : base(storageOptions, RewardCycleTable)
        {
            if (storageOptions == null)
            {
                throw new ArgumentNullException(nameof(storageOptions));
            }

            this.logger = logger;
        }

        /// <summary>
        /// This method is used to fetch active reward cycle details for a given team Id.
        /// </summary>
        /// <param name="teamId">Team Id.</param>
        /// <returns>Reward cycle for a given team Id.</returns>
        public async Task<RewardCycleEntity> GetActiveRewardCycleAsync(string teamId)
        {
            await this.EnsureInitializedAsync();
            string filterTeamId = TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, teamId);
            string filterActiveCycle = TableQuery.GenerateFilterConditionForInt("RewardCycleState", QueryComparisons.Equal, (int)RewardCycleState.Active);
            string filterInActiveCycle = TableQuery.GenerateFilterConditionForInt("RewardCycleState", QueryComparisons.Equal, (int)RewardCycleState.InActive);
            string filterPublish = TableQuery.GenerateFilterConditionForInt("ResultPublished", QueryComparisons.Equal, (int)ResultPublishState.Unpublished);
            string combineFilter = TableQuery.CombineFilters(filterInActiveCycle, TableOperators.And, filterPublish);
            string filter = TableQuery.CombineFilters(filterTeamId, TableOperators.And, TableQuery.CombineFilters(filterActiveCycle, TableOperators.Or, combineFilter));
            var query = new TableQuery<RewardCycleEntity>().Where(filter);
            TableContinuationToken continuationToken = null;
            var cycles = new List<RewardCycleEntity>();

            do
            {
                var queryResult = await this.CloudTable.ExecuteQuerySegmentedAsync(query, continuationToken);
                cycles.AddRange(queryResult?.Results);
                continuationToken = queryResult?.ContinuationToken;
            }
            while (continuationToken != null);

            return cycles.FirstOrDefault();
        }

        /// <summary>
        /// This method is used to fetch published reward cycle details for a given team Id.
        /// </summary>
        /// <param name="teamId">Team Id.</param>
        /// <returns>Reward cycle for a given team Id.</returns>
        public async Task<RewardCycleEntity> GetPublishedRewardCycleAsync(string teamId)
        {
            await this.EnsureInitializedAsync();
            string filterTeamId = TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, teamId);
            string filterPublish = TableQuery.GenerateFilterConditionForInt("ResultPublished", QueryComparisons.Equal, (int)ResultPublishState.Published);
            string filter = TableQuery.CombineFilters(filterTeamId, TableOperators.And, filterPublish);
            var query = new TableQuery<RewardCycleEntity>().Where(filter);
            TableContinuationToken continuationToken = null;
            var cycles = new List<RewardCycleEntity>();

            do
            {
                var queryResult = await this.CloudTable.ExecuteQuerySegmentedAsync(query, continuationToken);
                cycles.AddRange(queryResult?.Results);
                continuationToken = queryResult?.ContinuationToken;
            }
            while (continuationToken != null);

            return cycles.OrderByDescending(row => row.ResultPublishedOn).FirstOrDefault();
        }

        /// <summary>
        /// Store or update reward cycle in table storage.
        /// </summary>
        /// <param name="rewardCycleEntity">Represents reward cycle entity used for storage and retrieval.</param>
        /// <returns><see cref="Task"/> that represents reward cycle entity is saved or updated.</returns>
        public async Task<RewardCycleEntity> StoreOrUpdateRewardCycleAsync(RewardCycleEntity rewardCycleEntity)
        {
            await this.EnsureInitializedAsync();
            TableOperation addOrUpdateOperation = TableOperation.InsertOrReplace(rewardCycleEntity);
            var result = await this.CloudTable.ExecuteAsync(addOrUpdateOperation);
            return result.Result as RewardCycleEntity;
        }

        /// <summary>
        /// This method is used get active award cycle details for all teams.
        /// </summary>
        /// <returns><see cref="Task"/> that represents reward cycle entity is saved or updated.</returns>
        public async Task<List<RewardCycleEntity>> GetActiveAwardCycleForAllTeamsAsync()
        {
            await this.EnsureInitializedAsync();

            // Get all active reward cycle
            string filterActiveCycle = TableQuery.GenerateFilterConditionForInt("RewardCycleState", QueryComparisons.Equal, (int)RewardCycleState.Active);
            var query = new TableQuery<RewardCycleEntity>().Where(filterActiveCycle);
            TableContinuationToken continuationToken = null;
            var activeCycles = new List<RewardCycleEntity>();

            do
            {
                var queryResult = await this.CloudTable.ExecuteQuerySegmentedAsync(query, continuationToken);
                activeCycles.AddRange(queryResult?.Results);
                continuationToken = queryResult?.ContinuationToken;
            }
            while (continuationToken != null);
            return activeCycles as List<RewardCycleEntity>;
        }

        /// <summary>
        /// This method is used to start reward cycle is the start date matches the current date and stops the reward cycle based on the flags.
        /// </summary>
        /// <returns><see cref="Task"/> that represents reward cycle entity is saved or updated.</returns>
        public async Task<bool> UpdateCycleStatusAsync()
        {
            await this.EnsureInitializedAsync();

            var query = new TableQuery<RewardCycleEntity>();
            TableContinuationToken continuationToken = null;
            var activeCycles = new List<RewardCycleEntity>();

            do
            {
                var queryResult = await this.CloudTable.ExecuteQuerySegmentedAsync(query, continuationToken);
                activeCycles.AddRange(queryResult?.Results);
                continuationToken = queryResult?.ContinuationToken;
            }
            while (continuationToken != null);

            activeCycles = activeCycles.GroupBy(row => row.TeamId, (key, group) => group.OrderByDescending(rewardCycle => rewardCycle.Timestamp).FirstOrDefault()).ToList();

            // update reward cycle state
            foreach (RewardCycleEntity currentCycle in activeCycles)
            {
                var newCycle = this.SetAwardCycle(currentCycle);

                TableOperation updateOperation = TableOperation.InsertOrReplace(newCycle);
                await this.CloudTable.ExecuteAsync(updateOperation);
                this.logger.LogInformation($"Reward cycle set to {(RewardCycleState)newCycle.RewardCycleState} TeamId: {newCycle.TeamId}");
            }

            return true;
        }

        /// <summary>
        /// Set current reward cycle
        /// </summary>
        /// <param name="currentCycle">Current reward cycle for team</param>
        /// <returns>Returns updated reward cycle entity</returns>
        private RewardCycleEntity SetAwardCycle(RewardCycleEntity currentCycle)
        {
            DateTime currentUtcTime = DateTime.UtcNow;
            if (currentCycle.IsRecurring == (int)RecurringState.NonRecursive)
            {
                // current date should be between start date and end date
                if (currentUtcTime >= currentCycle.RewardCycleStartDate.Date
                    && currentUtcTime <= currentCycle.RewardCycleEndDate.Date
                    && currentCycle.ResultPublished != (int)ResultPublishState.Published)
                {
                    currentCycle.RewardCycleState = (int)RewardCycleState.Active;
                }
                else
                {
                    currentCycle.RewardCycleState = (int)RewardCycleState.InActive;
                }
            }
            else
            {
                var occurrenceType = (OccurrenceType)currentCycle.RangeOfOccurrence;

                switch (occurrenceType)
                {
                    case OccurrenceType.NoEndDate:
                        if (currentUtcTime > currentCycle.RewardCycleEndDate.Date)
                        {
                            // set a new award cycle for same duration.
                            this.GetNewCycle(currentCycle);
                        }

                        break;
                    case OccurrenceType.EndDate:
                        currentCycle.RangeOfOccurrenceEndDate = currentCycle.RangeOfOccurrenceEndDate?.Date.ToUniversalTime();
                        int cycleDurationInDays = (currentCycle.RewardCycleEndDate.Date - currentCycle.RewardCycleStartDate.Date).Days;
                        int? remainingDaysInOccurrenceEndDate = (currentCycle.RangeOfOccurrenceEndDate?.Date - currentUtcTime)?.Days;

                        if (currentUtcTime <= currentCycle.RewardCycleEndDate.Date
                            && currentUtcTime >= currentCycle.RewardCycleStartDate.Date
                            && currentCycle.ResultPublished != (int)ResultPublishState.Published)
                        {
                            currentCycle.RewardCycleState = (int)RewardCycleState.Active;
                        }
                        else if (currentUtcTime > currentCycle.RewardCycleEndDate.Date
                            && currentUtcTime <= currentCycle.RangeOfOccurrenceEndDate?.Date
                            && remainingDaysInOccurrenceEndDate > cycleDurationInDays)
                        {
                            // set a new award cycle for same duration till occurrence end date.
                            this.GetNewCycle(currentCycle);
                        }
                        else
                        {
                            currentCycle.RewardCycleState = (int)RewardCycleState.InActive;
                        }

                        break;
                    case OccurrenceType.Occurrence:
                        if (currentCycle.NumberOfOccurrences > 0
                            && (currentUtcTime > currentCycle.RewardCycleEndDate.Date))
                        {
                            this.GetNewCycle(currentCycle);
                            currentCycle.NumberOfOccurrences -= 1;
                        }
                        else if (currentCycle.NumberOfOccurrences >= 0 &&
                            currentUtcTime <= currentCycle.RewardCycleEndDate.Date
                            && currentCycle.ResultPublished != (int)ResultPublishState.Published)
                        {
                            currentCycle.RewardCycleState = (int)RewardCycleState.Active;
                        }
                        else
                        {
                            currentCycle.RewardCycleState = (int)RewardCycleState.InActive;
                        }

                        break;
                }
            }

            return currentCycle;
        }

        /// <summary>
        /// Set current reward cycle
        /// </summary>
        /// <param name="currentCycle">Current reward cycle for team</param>
        /// <returns>Returns updated reward cycle entity</returns>
        private RewardCycleEntity GetNewCycle(RewardCycleEntity currentCycle)
        {
            var guidValue = Guid.NewGuid().ToString();
            int cycleDurationInDays = (currentCycle.RewardCycleEndDate.Date - currentCycle.RewardCycleStartDate.Date).Days;

            currentCycle.CreatedOn = DateTime.UtcNow;
            currentCycle.CycleId = guidValue;
            currentCycle.ResultPublished = (int)ResultPublishState.Unpublished;
            currentCycle.RewardCycleEndDate = DateTime.UtcNow.AddDays(cycleDurationInDays);
            currentCycle.RewardCycleStartDate = DateTime.UtcNow;
            currentCycle.RewardCycleState = (int)RewardCycleState.Active;
            currentCycle.RowKey = guidValue;

            return currentCycle;
        }
    }
}