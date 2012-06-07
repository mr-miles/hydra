﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Threading;
using System.Windows.Forms;
using Bollywell.Hydra.Messaging;
using Bollywell.Hydra.Messaging.Config;
using Bollywell.Hydra.Messaging.MessageFetchers;
using Bollywell.Hydra.Messaging.Pollers;
using Bollywell.Hydra.Messaging.Serializers;
using Bollywell.Hydra.PubSubByType;
using HydraStressTestDtos;

namespace HydraStressTestConsole
{
    public partial class MainFrm : Form
    {
        private static HydraService _hydraService;
        private readonly Dictionary<string, ClientControl> _clients = new Dictionary<string, ClientControl>();
        private readonly Subscriber<StressTestData> _dataSubscriber;
        private IDisposable _dataSubscription;
        private readonly Subscriber<StressTestError> _errorSubscriber;
        private IDisposable _errorSubscription;
        private static readonly ISerializer<StressTestControl> Serializer = new HydraDataContractSerializer<StressTestControl>();
        private readonly IPoller<HydraMessage> _poller;
        private IDisposable _controlSubscription;

        public MainFrm()
        {
            InitializeComponent();

            WebRequest.DefaultWebProxy = null;
            var pinger = new Pinger(ConfigurationManager.AppSettings["HydraServers"].Split(',').Select(s => s.Trim()));
            pinger.RefreshSync();
            var servers = pinger.ServerInfo.Where(si => si.IsReachable).Select(si => si.Address);

            string pollSetting = ConfigurationManager.AppSettings["PollIntervalMs"];
            int? pollIntervalMs = pollSetting == null ? (int?)null : int.Parse(pollSetting);
            _hydraService = new HydraService(new RoundRobinConfigProvider(servers, ConfigurationManager.AppSettings["Database"], pollIntervalMs));

            _dataSubscriber = new Subscriber<StressTestData>(_hydraService);
            _dataSubscription = _dataSubscriber.ObserveOn(SynchronizationContext.Current).Subscribe(OnDataRecv);
            _errorSubscriber = new Subscriber<StressTestError>(_hydraService);
            _errorSubscription = _errorSubscriber.ObserveOn(SynchronizationContext.Current).Subscribe(OnErrorRecv);
            _poller = _hydraService.GetPoller(new HydraByTopicByDestinationMessageFetcher(typeof(StressTestControl).FullName, "StressTestConsole"));
            _controlSubscription = _poller.ObserveOn(SynchronizationContext.Current).Subscribe(OnControlRecv);

        }

        internal static void Send(StressTestControl message, string destination)
        {
            _hydraService.Send(new HydraMessage {Destination = destination, Topic = typeof(StressTestControl).FullName, Source = "StressTestConsole", Data = Serializer.Serialize(message)});
        }

        private void OnDataRecv(StressTestData message)
        {
            GetClientControl(message.Sender).DataMessage(message);
        }

        private void OnErrorRecv(StressTestError message)
        {
            GetClientControl(message.Receiver).ErrorMessage(message);
        }

        private void OnControlRecv(HydraMessage message)
        {
            GetClientControl(message.Source).ControlMessage(Serializer.Deserialize(message.Data));
        }

        private ClientControl GetClientControl(string clientId)
        {
            ClientControl res;
            if (!_clients.TryGetValue(clientId, out res)) {
                _clients.Add(clientId, new ClientControl());
                res = _clients[clientId];
                res.ClientId = clientId;
                ClientPanel.Controls.Add(res);
            }
            return res;
        }
    }
}
