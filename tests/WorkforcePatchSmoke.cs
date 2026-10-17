using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using HarmonyLib;

// Runs under the game's .NET Framework runtime, checking real Harmony field
// injection and finalizer execution. Unity-only PreUpdate work is stubbed out.
public static class WorkforcePatchSmoke
{
    private static int expectedActiveFactories;
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    public static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs request)
        {
            string file = new AssemblyName(request.Name).Name + ".dll";
            foreach (string directory in args)
            {
                string path = System.IO.Path.Combine(directory, file);
                if (System.IO.File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        };
        try { Run(); return 0; }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run()
    {
        Assembly mod = Assembly.Load("InfraSpaceQoL");
        LaserAutomationSmoke.Run(mod);
        Type patches = mod.GetType("InfraSpaceQoL.WorkforcePatches", true);
        Harmony harmony = new Harmony("InfraSpaceQoL.WorkforcePatchSmoke");
        try
        {
            Type research = mod.GetType("InfraSpaceQoL.ResearchPatches", true);
            harmony.Patch(AccessTools.Method(typeof(ProductionDefinitionManager), "GetProductionDefinition"),
                null, new HarmonyMethod(research.GetMethod("RedScienceOutputPostfix")));
            harmony.Patch(AccessTools.Method(typeof(Market), "CheckEmployment"),
                new HarmonyMethod(patches.GetMethod("CheckEmploymentPrefix")), null, null,
                new HarmonyMethod(patches.GetMethod("CheckEmploymentFinalizer")));
            harmony.Patch(AccessTools.Method(typeof(Market), "PreUpdate"),
                new HarmonyMethod(typeof(WorkforcePatchSmoke).GetMethod("HeadlessPreUpdate")));

            Resource.Init(delegate { return 0; });
            Resource resource = (Resource)FormatterServices.GetUninitializedObject(typeof(Resource));
            resource.stackSize = 100;
            var definitions = new ProductionDefinitionManager();
            var recipe = new ProductionDefinition();
            // Exercise patch installation/result injection without Unity's
            // resource registry and modified Newtonsoft assembly. Native pack
            // yields are covered by RedScienceOutputRegression.ps1.
            definitions.productionDefinitions.Add("test-red-science", recipe);
            for (int lookup = 0; lookup < 3; lookup++)
            {
                ProductionDefinition result = definitions.GetProductionDefinition("test-red-science");
                if (!ReferenceEquals(result, recipe))
                    throw new Exception("Harmony Red Science patch changed an empty recipe.");
            }
            Console.WriteLine("PASS: .NET Framework Harmony Red Science patch installation and result injection.");
            FactoryProductionLogic factory = new FactoryProductionLogic();
            factory.productionDefinition = new ProductionDefinition();
            factory.productionDefinition.producables = new List<ResourceCost> { new ResourceCost(resource, 1) };
            typeof(FactoryProductionLogic).GetField("maxWorkersCached", PrivateInstance).SetValue(factory, 4);
            factory.consumerProducer = (ConsumerProducer)FormatterServices.GetUninitializedObject(typeof(ConsumerProducer));
            factory.consumerProducer.outgoingStorage = new[] { 100 };
            Worker worker = (Worker)FormatterServices.GetUninitializedObject(typeof(Worker));
            factory.AddWorker(worker);
            Market market = new Market();
            var industries = Field<Dictionary<Resource, List<FactoryProductionLogic>>>(market, "industries");
            industries.Add(resource, new List<FactoryProductionLogic> { factory });
            var minHeaps = Field<Dictionary<Resource, Heap<FactoryProductionLogic>>>(market, "industriesMinHeap");
            var maxHeaps = Field<Dictionary<Resource, Heap<FactoryProductionLogic>>>(market, "industriesMaxHeap");
            foreach (var pair in new[] { Tuple.Create(minHeaps, "FactoryMinHeapComparer"), Tuple.Create(maxHeaps, "FactoryMaxHeapComparer") })
            {
                var comparer = (HeapComparer<FactoryProductionLogic>)Activator.CreateInstance(
                    typeof(Market).GetNestedType(pair.Item2, BindingFlags.NonPublic), true);
                pair.Item1.Add(resource, new Heap<FactoryProductionLogic>(comparer));
                pair.Item1[resource].Add(factory);
            }
            Field<Dictionary<Resource, int>>(market, "currentlyWorkingWorkersPerIndustry").Add(resource, 1);
            expectedActiveFactories = 0;
            market.CheckEmployment(0);
            if (factory.workers.Count != 0 || worker.workplace != null ||
                Field<List<Worker>>(market, "unemployedWorkers").Count != 1 ||
                !ReferenceEquals(industries, Field<object>(market, "industries")) ||
                minHeaps[resource].Count() != 1 || maxHeaps[resource].Count() != 1)
                throw new Exception("Harmony worker release or registry restoration failed.");

            factory.consumerProducer.outgoingStorage[0] = 99;
            expectedActiveFactories = 1;
            market.CheckEmployment(0);
            Console.WriteLine("PASS: .NET Framework Harmony worker-release patch and automatic resumption.");
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }

    private static T Field<T>(Market market, string name)
    {
        return (T)typeof(Market).GetField(name, PrivateInstance).GetValue(market);
    }

    public static bool HeadlessPreUpdate(Dictionary<Resource, List<FactoryProductionLogic>> ___industries)
    {
        int count = 0;
        foreach (var factories in ___industries.Values) count += factories.Count;
        if (count != expectedActiveFactories) throw new Exception("Unexpected factories offered to employment balancing.");
        return false;
    }
}
