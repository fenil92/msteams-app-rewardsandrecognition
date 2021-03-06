// <copyright file="RewardCycleEntity.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace Microsoft.Teams.Apps.RewardAndRecognition.Models
{
    using System;
    using Microsoft.WindowsAzure.Storage.Table;

    /// <summary>
    /// Reward cycle storage entity.
    /// </summary>
    public class RewardCycleEntity : TableEntity
    {
        /// <summary>
        /// Gets or sets team id.
        /// </summary>
        public string TeamId
        {
            get { return this.PartitionKey; }
            set { this.PartitionKey = value; }
        }

        /// <summary>
        /// Gets or sets reward cycle id.
        /// </summary>
        public string CycleId
        {
            get { return this.RowKey; }
            set { this.RowKey = value; }
        }

        /// <summary>
        /// Gets or sets start date of reward cycle.
        /// </summary>
        public DateTime RewardCycleStartDate { get; set; }

        /// <summary>
        /// Gets or sets end date of reward cycle.
        /// </summary>
        public DateTime RewardCycleEndDate { get; set; }

        /// <summary>
        /// Gets or sets number of occurrences of each reward cycle.
        /// </summary>
        public int NumberOfOccurrences { get; set; }

        /// <summary>
        /// Gets or sets state of recurring. Integer value. 0 = No / 1 = Yes.
        /// </summary>
        public int IsRecurring { get; set; }

        /// <summary>
        /// Gets or sets the state of occurrence. Integer value. 0 = NoEndDate / 1 = EndDate / 2= Occurrence.
        /// </summary>
        public int RangeOfOccurrence { get; set; }

        /// <summary>
        /// Gets or sets end date of occurrence.
        /// </summary>
        public DateTime? RangeOfOccurrenceEndDate { get; set; }

        /// <summary>
        /// Gets or sets current state of reward cycle. Integer value. 0 = Inactive / 1 =Active.
        /// </summary>
        public int RewardCycleState { get; set; }

        /// <summary>
        /// Gets or sets email address of the admin who created the reward cycle.
        /// </summary>
        public string CreatedByPrincipalName { get; set; }

        /// <summary>
        /// Gets or sets object id of the admin who created the reward cycle.
        /// </summary>
        public string CreatedByObjectId { get; set; }

        /// <summary>
        /// Gets or sets the date time of award cycle creation.
        /// </summary>
        public DateTime CreatedOn { get; set; }

        /// <summary>
        /// Gets or sets the state of reward publish for current reward cycle. 0 = False / 1 = true.
        /// </summary>
        public int ResultPublished { get; set; }

        /// <summary>
        /// Gets or sets the date time of award publish.
        /// </summary>
        public DateTime? ResultPublishedOn { get; set; }
    }
}