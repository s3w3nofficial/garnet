﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using BenchmarkDotNet.Attributes;
using Garnet.server;

namespace BDN.benchmark.Lua
{
    /// <summary>
    /// Benchmark for Lua
    /// </summary>
    [MemoryDiagnoser]
    public unsafe class LuaScripts
    {
        /// <summary>
        /// Lua parameters
        /// </summary>
        [ParamsSource(nameof(LuaParamsProvider))]
        public LuaParams Params { get; set; }

        /// <summary>
        /// Lua parameters provider
        /// </summary>
        public IEnumerable<LuaParams> LuaParamsProvider()
        {
            yield return new();
        }

        LuaRunner r1, r2, r3, r4;
        readonly string[] keys = ["key1"];

        [GlobalSetup]
        public void GlobalSetup()
        {
            r1 = new LuaRunner("return");
            r1.CompileForRunner();
            r2 = new LuaRunner("return 1 + 1");
            r2.CompileForRunner();
            r3 = new LuaRunner("return KEYS[1]");
            r3.CompileForRunner();
            r4 = new LuaRunner("return redis.call(KEYS[1])");
            r4.CompileForRunner();
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            r1.Dispose();
            r2.Dispose();
            r3.Dispose();
            r4.Dispose();
        }

        [Benchmark]
        public void Script1()
            => r1.RunForRunner();

        [Benchmark]
        public void Script2()
            => r2.RunForRunner();

        [Benchmark]
        public void Script3()
            => r3.RunForRunner(keys, null);

        [Benchmark]
        public void Script4()
            => r4.RunForRunner(keys, null);
    }
}