﻿// Copyright (c) September 2023, devMobile Software
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
namespace PayloadFormatter // Additional namespace for shortening interface when usage in formatter code
{
   using System.Collections.Generic;

   using Newtonsoft.Json.Linq;

   public interface IFormatterUplink
   {
      public JObject Evaluate(IDictionary<string, string> properties, string terminalId, DateTime timestamp, byte[] payloadBytes);
   }

   public interface IFormatterDownlink
   {
      public byte[] Evaluate(IDictionary<string, string> properties, string terminalId, JObject? payloadJson);

      public byte[] Evaluate(string terminalId, JObject? payloadJson, byte[] payloadBytes);
   }
}