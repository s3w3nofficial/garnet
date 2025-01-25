﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Garnet.cluster;
using Garnet.common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using NUnit.Framework.Internal;
using StackExchange.Redis;

namespace Garnet.test.cluster
{
    [TestFixture, NonParallelizable]
    internal class ClusterConfigTests
    {
        ClusterTestContext context;

        readonly Dictionary<string, LogLevel> monitorTests = [];

        [SetUp]
        public void Setup()
        {
            context = new ClusterTestContext();
            context.Setup(monitorTests);
        }

        [TearDown]
        public void TearDown()
        {
            context.TearDown();
        }

        [Test, Order(1)]
        [Category("CLUSTER-CONFIG"), CancelAfter(1000)]
        public void ClusterConfigInitializesUnassignedWorkerTest()
        {
            ClusterConfig config = new ClusterConfig().InitializeLocalWorker(
                Generator.CreateHexId(),
                "127.0.0.1",
                7001,
                configEpoch: 0,
                NodeRole.PRIMARY,
                null,
                "");

            (string address, int port) = config.GetWorkerAddress(0);
            Assert.That(address == "unassigned");
            Assert.That(port == 0);
            Assert.That(NodeRole.UNASSIGNED == config.GetNodeRoleFromNodeId("asdasdqwe"));

            var configBytes = config.ToByteArray();
            var restoredConfig = ClusterConfig.FromByteArray(configBytes);

            (address, port) = restoredConfig.GetWorkerAddress(0);
            Assert.That(address == "unassigned");
            Assert.That(port == 0);
            Assert.That(NodeRole.UNASSIGNED == restoredConfig.GetNodeRoleFromNodeId("asdasdqwe"));
        }

        [Test, Order(2)]
        [Category("CLUSTER-CONFIG"), CancelAfter(1000)]
        public async Task ClusterForgetAfterNodeRestartTest()
        {
            int nbInstances = 4;
            context.CreateInstances(4);
            context.CreateConnection();
            var (shards, slots) = context.clusterTestUtils.SimpleSetupCluster(logger: context.logger);

            // Restart node with new ACL file
            await context.nodes[0].StopAsync();
            context.nodes[0].Dispose(); ;
            context.nodes[0] = context.CreateInstance(context.clusterTestUtils.GetEndPoint(0).Port, useAcl: true, cleanClusterConfig: false);
            await context.nodes[0].RunAsync();
            context.CreateConnection();

            var firstNode = context.nodes[0];
            var nodesResult = context.clusterTestUtils.ClusterNodes(0);
            Assert.That(nodesResult.Nodes.Count == 4);

            try
            {
                var server = context.clusterTestUtils.GetServer(context.endpoints[0].ToIPEndPoint());
                var args = new List<object>() {
                    "forget",
                    Encoding.ASCII.GetBytes("1ip23j89123no"),
                    Encoding.ASCII.GetBytes("0")
                };
                var result = (string)server.Execute("cluster", args);
                Assert.Fail("Cluster forget call shouldn't have succeeded for an invalid node id.");
            }
            catch (Exception ex)
            {
                Assert.That(ex.Message == "ERR I don't know about node 1ip23j89123no.");
            }

            nodesResult = context.clusterTestUtils.ClusterNodes(0);
            Assert.That(nodesResult.Nodes.Count == 4, "No node should've been removed from the cluster after an invalid id was passed.");
            Assert.That(nodesResult.Nodes.ElementAt(0).IsMyself);
            Assert.That(nodesResult.Nodes.ElementAt(0).EndPoint.ToIPEndPoint().Port == 7000, "Expected the node to be replying to be the one with port 7000.");

            context.clusterTestUtils.ClusterForget(0, nodesResult.Nodes.Last().NodeId, 0);
            nodesResult = context.clusterTestUtils.ClusterNodes(0);
            Assert.That(nodesResult.Nodes.Count == 3, "A node should've been removed from the cluster.");
            Assert.That(nodesResult.Nodes.ElementAt(0).IsMyself);
            Assert.That(nodesResult.Nodes.ElementAt(0).EndPoint.ToIPEndPoint().Port == 7000, "Expected the node to be replying to be the one with port 7000.");
        }
    }
}