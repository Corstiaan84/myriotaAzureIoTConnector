﻿// Copyright (c) October 2023, devMobile Software
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
//---------------------------------------------------------------------------------
using System;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Azure.Devices.Client;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using PayloadFormatter;


namespace devMobile.IoT.MyriotaAzureIoTConnector.Connector
{
   internal class IoTHubDownlink : IIoTHubDownlink
   {
      private readonly ILogger<IoTHubDownlink> _logger;
      private readonly IPayloadFormatterCache _payloadFormatterCache;
      private readonly IMyriotaModuleAPI _myriotaModuleAPI;

      public IoTHubDownlink(ILogger<IoTHubDownlink> logger, IPayloadFormatterCache payloadFormatterCache, IMyriotaModuleAPI myriotaModuleAPI)
      {
         _logger = logger;
         _payloadFormatterCache = payloadFormatterCache;
         _myriotaModuleAPI = myriotaModuleAPI;
      }

      public async Task AzureIoTHubMessageHandler(Message message, object userContext)
      {
         Models.DeviceConnectionContext context = (Models.DeviceConnectionContext)userContext;

         _logger.LogInformation("Downlink- IoT Hub TerminalId:{TermimalId} LockToken:{lockToken}", context.TerminalId, message.LockToken);

         // broken out so using for message only has to be inside try
         string lockToken = message.LockToken; 

         try
         {
            using (message)
            {
               // Use default formatter and replace with message specific formatter if configured.
               string payloadFormatterName;
               if (!message.Properties.TryGetValue(Constants.IoTHubDownlinkPayloadFormatterProperty, out payloadFormatterName) || string.IsNullOrEmpty(payloadFormatterName))
               {
                  payloadFormatterName = context.PayloadFormatterDownlink;
               }

               _logger.LogInformation("Downlink- IoT Hub TerminalID:{TermimalId} LockToken:{lockToken} Payload formatter:{payloadFormatterName} ", context.TerminalId, lockToken, payloadFormatterName);

               // If this fails payload badly broken
               byte[] messageBytes = message.GetBytes();

               // These will fail for some messages, then payload formatter gets bytes only
               string messageText = string.Empty;
               JObject? messageJson = null;
               try
               {
                  messageText = Encoding.UTF8.GetString(messageBytes);

                  messageJson = JObject.Parse(messageText);
               }
               catch (ArgumentException aex)
               {
                  _logger.LogInformation("Downlink- IoT Hub TerminalID:{TerminalId} LockToken:{lockToken} messageBytes:{messageBytes} not valid text exception:{Message}", context.TerminalId, lockToken, BitConverter.ToString(messageBytes), aex.Message);
               }
               catch (JsonReaderException jex)
               {
                  _logger.LogInformation("Downlink- IoT Hub TerminalID:{TerminalId} LockToken:{lockToken} messageText:{messageText} not valid json exception:{Message}", context.TerminalId, lockToken, messageText, jex.Message);
               }

               // This shouldn't fail, but it could for lots of diffent reasons, invalid path to blob, syntax error, interface broken etc.
               IFormatterDownlink payloadFormatter = await _payloadFormatterCache.DownlinkGetAsync(payloadFormatterName);

               // This shouldn't fail, but it could for lots of different reasons, null references, divide by zero, out of range etc.
               byte[] payloadBytes = payloadFormatter.Evaluate(message.Properties, context.TerminalId, messageJson, messageBytes);

               // Validate payload before calling Myriota control message send API method
               if (payloadBytes is null)
               {
                  _logger.LogWarning("Downlink- IoT Hub TerminalID:{TerminalId} LockToken:{lockToken} payload formatter:{payloadFormatterName} Evaluate returned null", context.TerminalId, lockToken, payloadFormatterName);

                  await context.DeviceClient.RejectAsync(lockToken);

                  return;
               }

               if ((payloadBytes.Length < Constants.DownlinkPayloadMinimumLength) || (payloadBytes.Length > Constants.DownlinkPayloadMaximumLength))
               {
                  _logger.LogWarning("Downlink- IoT Hub TerminalID:{TerminalId} LockToken:{lockToken} payload formatter:{payloadFormatterName} returned payloadBytes:{payloadBytes} length:{Length} invalid must be {DownlinkPayloadMinimumLength} to {DownlinkPayloadMaximumLength} bytes", context.TerminalId, lockToken, payloadFormatterName, Convert.ToHexString(payloadBytes), payloadBytes.Length, Constants.DownlinkPayloadMinimumLength, Constants.DownlinkPayloadMaximumLength);

                  await context.DeviceClient.RejectAsync(lockToken);

                  return;
               }

               // This shouldn't fail, but it could few reasons mainly connectivity & myriota message queuing etc.
               _logger.LogInformation("Downlink- IoT Hub TerminalID:{TerminalId} LockToken:{lockToken} PayloadBytes:{payloadBytes} Length:{Length} sending", context.TerminalId, lockToken, Convert.ToHexString(payloadBytes), payloadBytes.Length);

               // Finally send the message using Myriota API
               string messageId = await _myriotaModuleAPI.SendAsync(context.TerminalId, payloadBytes);

               _logger.LogInformation("Downlink- IoT Hub TerminalID:{TerminalId} LockToken:{lockToken} MessageID:{messageId} sent", context.TerminalId, lockToken, messageId);

               await context.DeviceClient.CompleteAsync(lockToken);
            }
         }
         catch (Exception ex)
         {
            _logger.LogError(ex, "Downlink- IoT Hub TerminalID:{TerminalId} LockToken:{lockToken} MessageHandler processing failed", context.TerminalId, lockToken);

            await context.DeviceClient.RejectAsync(lockToken);
         }
      }
   }
}

