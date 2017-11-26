﻿using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Utilities;
using BreezeCommon;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
using Stratis.Bitcoin.Features.Notifications.Interfaces;

namespace Breeze.Registration
{
	public class RegistrationFeature : FullNodeFeature
	{
        private ILogger logger;
        private NodeSettings nodeSettings;
		private RegistrationStore registrationStore;
        private readonly ConcurrentChain chain;
        private readonly Signals signals;
        private IWatchOnlyWalletManager watchOnlyWalletManager;
	    private readonly IBlockNotification blockNotification;

        private ILoggerFactory loggerFactory;
        private readonly IRegistrationManager registrationManager;
        private IDisposable blockSubscriberdDisposable;
        //private IDisposable transactionSubscriberdDisposable;

        private bool isBitcoin;
        private Network network;

        public RegistrationFeature(ILoggerFactory loggerFactory,
            NodeSettings nodeSettings,
            RegistrationManager registrationManager,
            RegistrationStore registrationStore,
            ConcurrentChain chain,
            Signals signals,
            IWatchOnlyWalletManager watchOnlyWalletManager,
            IBlockNotification blockNotification)
		{
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.nodeSettings = nodeSettings;
            this.registrationManager = registrationManager;
			this.registrationStore = registrationStore;
            this.chain = chain;
            this.signals = signals;
            this.network = nodeSettings.Network;
            this.watchOnlyWalletManager = watchOnlyWalletManager;
		    this.blockNotification = blockNotification;

            if (nodeSettings.Network == Network.Main || nodeSettings.Network == Network.TestNet || nodeSettings.Network == Network.RegTest)
            {
                // Bitcoin networks - these currently only interrogate the registration store for initial masternode selection
                this.isBitcoin = true;
            }
            else
            {
                // Stratis networks - these write to the registration store as new registrations come in via blocks
                this.isBitcoin = false;

                // Force registration store to be kept in same folder as other node data
                this.registrationStore.SetStorePath(this.nodeSettings.DataDir);
            }
		}

		public override void Start()
		{
            if (!this.isBitcoin)
            {
                // Start syncing from the height of the earliest possible registration transaction instead of the genesis block. This saves time.
                //this.logger.LogDebug("Registration: blockNotification.StartHash = " + this.blockNotification.StartHash);
                if (true) // (this.blockNotification.StartHash == null)
                {
                    if (this.network == Network.StratisMain) // 616966
                        this.blockNotification.SyncFrom(uint256.Parse("d88a5b3734b991eca919b399cc676b4990f93a7b1b59aba0d42b13ffe7ec1169"));

                    if (this.network == Network.StratisTest) // 168994, first registration was in 168995
                        this.blockNotification.SyncFrom(uint256.Parse("120f5aab8a3b82ca273d4f3c5a8ae698d1d4135014ad0d813a16f9272c5dca58"));

                    // For regtest, it is not clear that re-issuing a sync command will be beneficial. Generally you want to sync from genesis in that case.
                }

                // Only need to subscribe to receive blocks and transactions on the Stratis network
                this.blockSubscriberdDisposable = this.signals.SubscribeForBlocks(new RegistrationBlockObserver(this.chain, this.registrationManager));
                //this.transactionSubscriberdDisposable = this.signals.SubscribeForTransactions(new TransactionObserver(this.registrationManager));
            }

            this.registrationManager.Initialize(this.loggerFactory, this.registrationStore, this.isBitcoin, this.network, this.watchOnlyWalletManager);
        }

        public override void Stop()
        {
            this.blockSubscriberdDisposable?.Dispose();
            //this.transactionSubscriberdDisposable?.Dispose();
        }
    }
    
	public static class RegistrationFeatureExtension
	{
		public static IFullNodeBuilder UseRegistration(this IFullNodeBuilder fullNodeBuilder)
		{
			fullNodeBuilder.ConfigureFeature(features =>
			{
				features
					.AddFeature<RegistrationFeature>()
					.FeatureServices(services =>
					{
						services.AddSingleton<RegistrationStore>();
                        services.AddSingleton<RegistrationManager>();
                    });
			});
			return fullNodeBuilder;
		}
	}
}