using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using CDWPlanner.DTO;
using CDWPlanner.Helpers;
using CDWPlanner.Model;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Logging;
using MongoDB.Bson.IO;

namespace CDWPlanner.Services
{
    public class ReminderService
    {
        private readonly ServiceBusConnection _serviceBusConnection;
        private readonly ILogger<PlanEvent> _logger;

        public ReminderService
        (
            ServiceBusConnection serviceBusConnection,
            ILogger<PlanEvent> logger
        )
        {
            _serviceBusConnection = serviceBusConnection;
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
        public async Task ScheduleCallback(Workshop dbWorkshop, DateTime eventDate, Workshop incomingWorkshop)
        {
            if (dbWorkshop != null && !WorkshopHelpers.TimeHasChanged(dbWorkshop, incomingWorkshop))
            {
                // No time has changed; Nothing to do here ¯\_(ツ)_/¯
                return;
            }

            if (!TimeSpan.TryParse(incomingWorkshop.begintime, out var beginTime))
            {
                // Cannot parse begintime, better stick with that, what we got i guess
                _logger.LogWarning($"Cannot parse begintime of {incomingWorkshop.title}: {incomingWorkshop.begintime}. " +
                                   $"No reminder will be (re)scheduled");
                return;
            }

            var beginDateTime = DateTime.SpecifyKind(eventDate + beginTime, DateTimeKind.Local).ToUniversalTime();
            if (beginDateTime < DateTime.UtcNow)
            {
                // Guess someone edited an old workshop
                return;
            }


            var topicClient = new TopicClient(_serviceBusConnection, "wakeuptimer", RetryPolicy.Default);

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
            var scheduledEnqueueTimeUtc = beginDateTime.AddMinutes(-15);
            var msg = new Message
            {
                ScheduledEnqueueTimeUtc = scheduledEnqueueTimeUtc,
                Body = Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(new CallbackMessage
                {
                    UniqueStateId = incomingWorkshop.uniqueStateId,
                    Date = beginDateTime
                }))
            };

            var seq = await topicClient.ScheduleMessageAsync(msg, scheduledEnqueueTimeUtc);
            incomingWorkshop.callbackMessageSequenceNumber = seq;
            Console.WriteLine("Enqueued new callback message!");
        }
    }
}