using Dao.Terrain.Generation;
using Dao.Terrain.Rendering;
using Godot;

namespace Dao.Terrain.Streaming;

public partial class TerrainWorld
{
    private static class TerrainWorldRuntimeLifecycleService
    {
        internal static void OnReady(TerrainWorld world)
        {
            world.Settings ??= new TerrainSettings();
            world._profile = world.Settings.Snapshot();
            world._hasProfileSnapshot = true;
            world.EnsureGeneratedWorldPlan();
            world.RebuildPlanIndices();
            if (world._profile.UseNativeSamplerWhenAvailable)
            {
                NativeTerrainBridge.EnsureInitialized();
                if (!NativeTerrainBridge.IsAvailable)
                {
                    GD.PushWarning("Native terrain sampler requested but unavailable; using managed C# sampler.");
                }
            }

            world._terrainMaterial = TerrainMaterialFactory.CreateTerrainMaterial();
            world._waterMaterial = TerrainMaterialFactory.CreateWaterMaterial();
            world._localWaterMaterial = TerrainMaterialFactory.CreateLocalWaterMaterial();

            world.ResolveFocus();

            if (world.CreateWaterPlane)
            {
                world.CreateWater();
            }

            world._isReady = true;
            if (world._worldPlan is not null)
            {
                world.EmitPlanReadySignalIfReady();
                world.MarkStreamingSnapshotDirty();
            }

            world.UpdateStreaming(force: true);
            world.EmitStreamingSnapshotChangedSignalIfNeeded();
        }

        internal static void OnProcess(TerrainWorld world, double delta)
        {
            world.DisposeCompletedRetiredJobs();
            world.SubmitCompletedWorldPlanJob();
            world.SubmitCompletedJobs();

            world._streamTimer += delta;
            if (world._streamTimer >= world.StreamingIntervalSeconds)
            {
                world._streamTimer = 0.0;
                world.UpdateStreaming(force: false);
            }

            world.UpdateWaterPlane();
            world.EmitStreamingSnapshotChangedSignalIfNeeded();
        }

        internal static void OnExitTree(TerrainWorld world)
        {
            world._worldPlanGenerationVersion++;
            world.CancelWorldPlanJob();
            world.CancelAllJobs();
            world.DisposeCompletedRetiredJobs();
        }

        internal static void SetFocus(TerrainWorld world, Node3D focus)
        {
            world._focus = focus;
            world.MarkStreamingSnapshotDirty();
            world.UpdateStreaming(force: true);
            world.EmitStreamingSnapshotChangedSignalIfNeeded();
        }

        internal static void SetWorldPlan(TerrainWorld world, TerrainWorldPlan? worldPlan)
        {
            bool hadPlan = world._worldPlan is not null;
            world.EnsureProfileSnapshot();
            world._worldPlanGenerationVersion++;
            world.CancelWorldPlanJob();
            world._worldPlan = worldPlan is null ? null : TerrainWorldPlan.CopyOf(worldPlan);
            world.ApplyPlanIndexChanges(hadPlan);
            world.EmitStreamingSnapshotChangedSignalIfNeeded();
        }

        internal static void Regenerate(TerrainWorld world)
        {
            bool hadPlan = world._worldPlan is not null;
            world.Settings ??= new TerrainSettings();
            world._profile = world.Settings.Snapshot();
            world._hasProfileSnapshot = true;
            if (world.GenerateOpenWorldPlanOnReady)
            {
                world._worldPlan = null;
                if (hadPlan)
                {
                    world.EmitPlanClearedSignalIfReady();
                    world.MarkStreamingSnapshotDirty();
                }

                world.PrepareGeneratedWorldPlan();
                world.RebuildPlanIndices();
                if (!world.GenerateOpenWorldPlanAsync && world._worldPlan is not null)
                {
                    world.EmitPlanReadySignalIfReady();
                    world.MarkStreamingSnapshotDirty();
                }
            }
            else
            {
                world.RebuildPlanIndices();
            }

            if (world._profile.UseNativeSamplerWhenAvailable)
            {
                NativeTerrainBridge.EnsureInitialized();
            }

            world.InvalidatePlanDependentStreamingState();
            world.UpdateStreaming(force: true);
            world.EmitStreamingSnapshotChangedSignalIfNeeded();
        }

        internal static void InvalidatePlanDependentStreamingState(TerrainWorld world)
        {
            world.CancelAllJobs();
            world.ClearTileCache();
            world.ClearChunks();
        }

        internal static TerrainWorldPlan GenerateOpenWorldPlan(TerrainWorld world, bool apply)
        {
            world.Settings ??= new TerrainSettings();
            if (!world._isReady)
            {
                world._profile = world.Settings.Snapshot();
                world._hasProfileSnapshot = true;
            }

            world._worldPlanGenerationVersion++;
            world.CancelWorldPlanJob();
            float worldSize = Mathf.Max(world._profile.ChunkSize, world.OpenWorldPlanWorldSize);
            TerrainWorldPlan plan = CreateRuntimeOpenWorldPlan(world._profile, worldSize);

            if (world.ValidateGeneratedOpenWorldPlan || world.PrintGeneratedOpenWorldPlanSummary)
            {
                world.ReportGeneratedOpenWorldPlan(plan);
            }

            if (apply)
            {
                world.SetWorldPlan(plan);
            }

            return plan;
        }

        internal static void EnsureGeneratedWorldPlan(TerrainWorld world)
        {
            if (!world.GenerateOpenWorldPlanOnReady || world._worldPlan is not null)
            {
                return;
            }

            world.PrepareGeneratedWorldPlan();
        }

        internal static void EnsureProfileSnapshot(TerrainWorld world)
        {
            if (world._hasProfileSnapshot)
            {
                return;
            }

            world.Settings ??= new TerrainSettings();
            world._profile = world.Settings.Snapshot();
            world._hasProfileSnapshot = true;
        }
    }
}
