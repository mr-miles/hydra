using System.Collections.Generic;
using LoveSeat.Interfaces;
using Newtonsoft.Json.Linq;
using Shastra.Hydra.Messaging.MessageIds;

namespace Shastra.Hydra.Messaging.Storage
{
    public interface IStore
    {
        string Name { get; }
        IEnumerable<IMessageId> GetChanges(IMessageId startId, long sinceSeq, out long lastSeq);
        long GetLastSeq();
        IMessageId SaveDoc(JObject json, IEnumerable<Attachment> attachments = null);
        IEnumerable<JToken> GetDocs(string viewName, IViewOptions options);
        ServerDistanceInfo MeasureDistance();
    }
}
