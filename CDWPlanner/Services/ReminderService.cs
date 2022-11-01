using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Azure.Core;
using Azure.Messaging.ServiceBus;

using CDWPlanner.DTO;
using CDWPlanner.Helpers;
using CDWPlanner.Model;

using Microsoft.Extensions.Logging;

using MongoDB.Bson.IO;

namespace CDWPlanner.Services
{
    public class ReminderService
    {
        private readonly ServiceBusClient _serviceBusClient;
        private readonly ILogger<PlanEvent> _logger;

        public ReminderService
        (
            ServiceBusClient serviceBusClient,
            ILogger<PlanEvent> logger
        )
        {
            _serviceBusClient = serviceBusClient;
            _logger = logger;
        }

        /// <summary>
        /// This thing has exactly 2 obligations:
        ///    1: Cancel old callback
        ///    2: Create new callback
        /// </summary>
        /// <param name="dbWorkshop"></param>
        /// <param name="eventDate"></param>
        /// <param name="incomingWorkshop"></param>
        /// <returns></returns>
        public async Task ScheduleCallback(Workshop dbWorkshop, string eventDate, Workshop incomingWorkshop)
        {
            if (dbWorkshop != null && !WorkshopHelpers.TimeHasChanged(dbWorkshop, incomingWorkshop))
            {
                var callbackExits = dbWorkshop.callbackMessageSequenceNumber != null;
                if (callbackExits)
                {
                    // No time has changed; Nothing to do here ¯\_(ツ)_/¯
                    return;
                }
            }

            var beginDate = ParseTime(eventDate, incomingWorkshop.begintime).ToUniversalTime();
            var scheduledEnqueueTimeUtc = beginDate.AddMinutes(-15);

            if (beginDate < DateTime.UtcNow || scheduledEnqueueTimeUtc < DateTime.UtcNow)
            {
                // Guess someone edited an old workshop
                _logger.LogInformation("Time was in the past");
                return;
            }

            //var cli = new ServiceBusClient(_serviceBusConnection, new ServiceBusClientOptions
            //{
            //    RetryOptions = new ServiceBusRetryOptions
            //    {
            //        Delay = TimeSpan.FromSeconds(1),
            //        MaxDelay = TimeSpan.FromSeconds(10),
            //        MaxRetries = 5,
            //        Mode = ServiceBusRetryMode.Exponential
            //    }
            //});
            var topicClient = _serviceBusClient.CreateSender("wakeuptimer");
            
            
            //var topicClient = new TopicClient(_serviceBusConnection, "wakeuptimer", RetryPolicy.Default);

            if (dbWorkshop?.callbackMessageSequenceNumber is { } oldSequenceNumber)
            {
                try
                {
                    await topicClient.CancelScheduledMessageAsync(oldSequenceNumber);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error while trying to cancel event");
                }
            }

            incomingWorkshop.uniqueStateId = Guid.NewGuid();
            var msg = new ServiceBusMessage(Newtonsoft.Json.JsonConvert.SerializeObject(new CallbackMessage
            {
                UniqueStateId = incomingWorkshop.uniqueStateId,
                Date = beginDate.DateTime
            }))
            {
                ScheduledEnqueueTime = scheduledEnqueueTimeUtc.DateTime, 
            };

            var seq = await topicClient.ScheduleMessageAsync(msg, scheduledEnqueueTimeUtc);
            incomingWorkshop.callbackMessageSequenceNumber = seq;
            _logger.LogInformation($"Enqueued new callback message at {scheduledEnqueueTimeUtc}");
        }

        /// <summary>
        /// Making date/time timezone aware hopefully fixes the problem....
        /// Switching from summer to winter-time could become a problem when the workshop starts exactly then
        /// ...but hopefully no kid will ever be forced to attend one of those horrible workshops at 0 am....
        /// </summary>
        /// <param name="date"></param>
        /// <param name="time"></param>
        /// <returns></returns>

        private static DateTimeOffset ParseTime(string date, string time)
        {
            var tz = TimeZoneInfo.GetSystemTimeZones().First(x => x.Id == "W. Europe Standard Time");
            var offset = tz.GetUtcOffset(DateTime.Parse(date));

            return DateTimeOffset.Parse($"{date} {time} +{offset}");
        }
    }
}