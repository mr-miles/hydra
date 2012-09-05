﻿using System;
using System.Runtime.Serialization;
using Bollywell.Hydra.Messaging.MessageIds;
using LoveSeat;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bollywell.Hydra.Messaging
{
    [DataContract]
    public abstract class TransportMessage : IComparable<TransportMessage>
    {
        public IMessageId MessageId { get; set; }
        [DataMember] public string Type
        {
            get { return "message"; }
            set { }
        }

        protected void SetFromCouchId(string couchId)
        {
            MessageId = MessageIdManager.Create(couchId);
        }

        /// <summary>
        /// Hydrate a CouchDb view row into a TMessage. The row has an id property, which is the document id, and a doc property which is the HydraMessage JSON.
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        public static TMessage Hydrate<TMessage>(JToken row) where TMessage : TransportMessage
        {
            var res = JsonConvert.DeserializeObject<TMessage>(row["doc"].ToString());
            res.SetFromCouchId((string)row["id"]);
            return res;
        }

        /// <summary>
        /// Serialise the message for sending to a Store.
        /// </summary>
        /// <returns>The message serialised to a JSON string</returns>
        /// <remarks>The type parameter to Document&lt;T&gt; is irrelevant as it is only used for deserialisation, and here we are only serialising.</remarks>
        internal string ToJson()
        {
            return new Document<TransportMessage>(this).ToString();
        }

        public int CompareTo(TransportMessage other)
        {
            return MessageId.CompareTo(other.MessageId);
        }
    }

}
