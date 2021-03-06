﻿using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Subjects;
using Shastra.Hydra.Messaging;
using Shastra.Hydra.Messaging.Listeners;
using Shastra.Hydra.Messaging.MessageFetchers;
using Shastra.Hydra.Messaging.Serializers;

namespace Shastra.Hydra.Conversations
{
    public class Switchboard<TMessage> : IObservable<Conversation<TMessage>>, IDisposable
    {
        // Maps handles to their conversations
        private readonly Dictionary<string, Conversation<TMessage>> _conversations = new Dictionary<string, Conversation<TMessage>>();
        private readonly HashSet<string> _deadConversations = new HashSet<string>();
        private readonly IListener<HydraMessage> _listener;
        private readonly Subject<Conversation<TMessage>> _subject = new Subject<Conversation<TMessage>>();
        private readonly ISerializer<TMessage> _serializer;
        private readonly IHydraService _hydraService;
        private readonly string _thisParty;
        private readonly string _topic;

        public long BufferDelayMs { get { return _listener.BufferDelayMs; } set { _listener.BufferDelayMs = value; } }

        /// <summary>
        /// Create a new Switchboard to listen for incoming conversations and initiate outgoing ones.
        /// </summary>
        /// <param name="hydraService">The HydraService with which this Switchboard communicates.</param>
        /// <param name="thisParty">Name of this end of the conversation. This will be the RemoteParty for anyone initiating a conversation with this app.</param>
        /// <param name="topic">Topic of the conversation.</param>
        /// <param name="serializer">Optional serialiser for messages. Defaults to DataContractSerializer.</param>
        public Switchboard(IHydraService hydraService, string thisParty, string topic = null, ISerializer<TMessage> serializer = null)
        {
            _hydraService = hydraService;
            _thisParty = thisParty;
            _topic = topic ?? typeof (TMessage).FullName;
            _serializer = serializer ?? new HydraDataContractSerializer<TMessage>();

            _listener = hydraService.GetListener(new HydraByTopicByDestinationMessageFetcher(_topic, thisParty));
            _listener.Subscribe(OnMessage);
        }

        /// <summary>
        /// Initiate a new conversation.
        /// </summary>
        /// <param name="remoteParty">The other party in the conversation.</param>
        /// <returns>The conversation</returns>
        public Conversation<TMessage> NewConversation(string remoteParty)
        {
            string handle = Guid.NewGuid().ToString("N");
            return CreateNewConversation(remoteParty, handle);
        }

        private void OnMessage(HydraMessage message)
        {
            string handle = message.Handle;
            if (_deadConversations.Contains(handle)) return;

            if (!_conversations.ContainsKey(handle)) {
                CreateNewConversation(message.Source, handle);
            }
            _conversations[handle].OnNext(message.Seq, MessageNotification(message.Data));
        }

        private Conversation<TMessage> CreateNewConversation(string remoteParty, string handle)
        {
            var conversation = new Conversation<TMessage>();
            conversation.DoneEvent += ConversationDoneEvent;
            conversation.BaseInit(_hydraService, _thisParty, remoteParty, _topic, handle, _serializer);
            _conversations[handle] = conversation;
            _subject.OnNext(conversation);
            return conversation;
        }

        private void ConversationDoneEvent(object obj)
        {
            var conversation = (Conversation<TMessage>) obj;
            conversation.DoneEvent -= ConversationDoneEvent;
            if (_conversations.ContainsKey(conversation.Handle)) _conversations.Remove(conversation.Handle);
            _deadConversations.Add(conversation.Handle);
        }

        private Notification<TMessage> MessageNotification(string data)
        {
            try
            {
                return Notification.CreateOnNext(_serializer.Deserialize(data));
            }
            catch (Exception ex)
            {
                return Notification.CreateOnError<TMessage>(ex);
            }
        }

        #region Implementation of IObservable<out TConversation>

        public IDisposable Subscribe(IObserver<Conversation<TMessage>> observer)
        {
            return _subject.Subscribe(observer);
        }

        #endregion

        #region Implementation of IDisposable

        // See http://msdn.microsoft.com/en-us/library/ms244737.aspx

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing) {
                // free managed resources
                _listener.Dispose();
                _subject.Dispose();
            }
            // free native resources if there are any.
        }

        #endregion

    }
}
