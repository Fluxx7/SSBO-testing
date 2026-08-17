using Godot;
using System;
using FluxxiShaderLang;
using Godot.Collections;

public partial class MeshTest : MeshInstance3D {
	private MeshRD test_mesh = new();

	[Export] public uint Subdivisions = 512;

	[Export] public Vector2 Size = new(500f, 500f);

	[Export] public Material Material;
	
	private FSLFile meshGenFile = FSLFile.FromFile("res://assets/Shaders/Compute/FSL/mesh/mesh_gen.fsl");
	private FSLFile cbTreeFile = FSLFile.FromFile("res://assets/Shaders/Compute/FSL/mesh/cbtrees.fsl");
	private ComputePlan cbtMeshGen = new();
	private ComputePlan initCBTMesh = ComputePlan.MakeNew();
	private ComputeKernel meshGen;
	private FSLVertexBuffer vertBuffer;
	private FSLIndexBuffer indexBuffer;
	private FSLStorageBuffer commandBuffer;
	private uint numVertices, numIndices;
	private uint vertsPerSide;
	private Rid vertBufferId, indexBufferId, commandBufferId;
	private bool rebuildQueued = false;
	
	[Export] public uint CBTDepth = 16;
	[Export] public uint CBTBaseMeshDivisions = 4;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready() {
		meshGen = meshGenFile.GetKernel("genSubdivMesh");
		vertsPerSide = Subdivisions + 2;
		numVertices = vertsPerSide * vertsPerSide;
		numIndices = (vertsPerSide - 1) * (vertsPerSide - 1) * 6;
		// InitShader();
		InitCBTrees();
		// RebuildMesh();
		Mesh = test_mesh;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta) {
		if (rebuildQueued) {
			RebuildMesh();
		}
	}

	private void InitShader() {
		vertBuffer = meshGen.GetVertexBuffer("VertexBuffer");
		indexBuffer = meshGen.GetIndexBuffer("IndexBuffer");
		commandBuffer = meshGen.GetStorageBuffer("IndirectCommandBuffer");
		
		commandBuffer.AddFlag((ulong) RenderingDevice.StorageBufferUsage.Indirect);
		
		vertBuffer.SetVertexCount(numVertices);
		vertBuffer.SetVertexSizeBytes(12);
		indexBuffer.SetIndexCount(numIndices);
		indexBuffer.SetIndexFormat(numVertices <= 65536 ? RenderingDevice.IndexBufferFormat.Uint16 : RenderingDevice.IndexBufferFormat.Uint32);
		
		vertBuffer.ConnectAndCall(Callable.From((Rid new_rid) => { 
			vertBufferId = new_rid;
			rebuildQueued = true;
		}));
		indexBuffer.ConnectAndCall(Callable.From((Rid new_rid) => { 
			indexBufferId = new_rid;
			rebuildQueued = true;
		}));
		commandBuffer.ConnectAndCall(Callable.From((Rid new_rid) => { 
			commandBufferId = new_rid;
			rebuildQueued = true;
		}));
		
		meshGen.Dispatch(vertsPerSide, vertsPerSide, 1, new Dictionary<StringName, Variant> {
			{"sizeX", Size.X},
			{"sizeY", Size.Y},
			{"subdivisions", Subdivisions}
		});
	}

	private void RebuildMesh() {
		test_mesh.ClearSurfaces();
		test_mesh.AddSurface(
			Mesh.ArrayFormat.FormatVertex | Mesh.ArrayFormat.FormatIndex,
			Mesh.PrimitiveType.Triangles,
			(int)numVertices,
			vertBufferId,
			new Aabb(new Vector3(-Size.X * 0.5f, -1f, -Size.Y * 0.5f),
				new Vector3(Size.X, 2f, Size.Y)),
			indexCount: (int)numIndices,
			indexBuffer: indexBufferId,
			material: Material,
			indirectBuffer: commandBufferId
		);
		rebuildQueued = false;
	}
	
	private void InitCBTrees() {
		ComputeGroup cbtGroup = cbTreeFile.GetKernelGroup();
		FSLStorageBuffer dispatchBuffer = cbtGroup.GetStorageBuffer("IndirectDispatchBuffer");
		
		vertsPerSide = CBTBaseMeshDivisions + 1;
		numVertices = 66536;
		numIndices = 66536;
		vertBuffer = cbtGroup.GetVertexBuffer("VertexBuffer");
		indexBuffer = cbtGroup.GetIndexBuffer("IndexBuffer");
		commandBuffer = cbtGroup.GetStorageBuffer("IndirectDrawCommandBuffer");
		
		vertBuffer.SetVertexCount(numVertices);
		vertBuffer.SetVertexSizeBytes(12);
		indexBuffer.SetIndexCount(numIndices);
		indexBuffer.SetIndexFormat(RenderingDevice.IndexBufferFormat.Uint32);
		
		vertBuffer.ConnectAndCall(Callable.From((Rid new_rid) => { 
			vertBufferId = new_rid;
			rebuildQueued = true;
		}));
		indexBuffer.ConnectAndCall(Callable.From((Rid new_rid) => { 
			indexBufferId = new_rid;
			rebuildQueued = true;
		}));
		commandBuffer.ConnectAndCall(Callable.From((Rid new_rid) => { 
			commandBufferId = new_rid;
			rebuildQueued = true;
		}));
		
		cbtGroup.GetStorageBuffer("BisectorBuffer").SetUnsizedElementCount((uint) Math.Pow(2, CBTDepth));
		cbtGroup.GetStorageBuffer("CBTreeBuffer").SetUnsizedElementCount((uint) Math.Pow(2, CBTDepth + 1));
		cbtGroup.GetStorageBuffer("CBTDataBuffer").SetUnsizedElementCount((uint) Math.Pow(2, CBTDepth));
		cbtGroup.GetVertexBuffer("VertexInputBuffer").SetVertexCount(66536);
		cbtGroup.GetVertexBuffer("VertexInputBuffer").SetVertexSizeBytes(12);
		cbtGroup.GetStorageBuffer("HalfEdgeBuffer")
			.SetUnsizedElementCount(4 * CBTBaseMeshDivisions * CBTBaseMeshDivisions);
		
		initCBTMesh.AddKernel(cbtGroup.GetKernel("initBuffers"), CBTBaseMeshDivisions, CBTBaseMeshDivisions, 1,
				new Dictionary<StringName, Variant> {
					{"sizeX", Size.X},
					{"sizeY", Size.Y},
					{ "cbt_depth_in", CBTDepth },
					{ "base_mesh_divisions", CBTBaseMeshDivisions }
				})
			.AddBarrier()
			.AddKernelWorkgroups(cbtGroup.GetKernel("postHalfEdge"), 1, 1, 1,
				new Dictionary<StringName, Variant> {
					{ "cbt_depth_in", CBTDepth }
				})
			.AddBarrier()
			.AddKernelIndirect(cbtGroup.GetKernel("makeRootBisectors"), dispatchBuffer, 0)
			.AddBarrier()
			.AddKernelIndirect(cbtGroup.GetKernel("buildTris"), dispatchBuffer, 0)
			.Dispatch();
		
		cbtMeshGen.AddKernel(cbtGroup.GetKernel("resetCounter"), 1, 1, 1)
			.AddBarrier()
			.AddKernelIndirect(cbtGroup.GetKernel("cachePointers"), dispatchBuffer, 0)
			.AddBarrier()
			.AddKernelIndirect(cbtGroup.GetKernel("resetCommands"), dispatchBuffer, 0)
			.AddBarrier()
			.AddKernelIndirect(cbtGroup.GetKernel("generateCommands"), dispatchBuffer, 0)
			.AddBarrier()
			.AddKernelIndirect(cbtGroup.GetKernel("reserveBlocks"), dispatchBuffer, 0)
			.AddBarrier()
			.AddKernelIndirect(cbtGroup.GetKernel("fillNewBlocks"), dispatchBuffer, 0)
			.AddBarrier()
			.AddKernelIndirect(cbtGroup.GetKernel("updateNeighbors"), dispatchBuffer, 0)
			.AddBarrier()
			.AddKernelIndirect(cbtGroup.GetKernel("updateBitfield"), dispatchBuffer, 0);
		for (var d = 0; d < CBTDepth; d++) {
			cbtMeshGen.AddBarrier().AddKernel(cbtGroup.GetKernel("sumReduction"), (uint)Math.Pow(2, d), 1, 1);
		}
		cbtMeshGen.AddBarrier().AddKernelIndirect(cbtGroup.GetKernel("buildTris"), dispatchBuffer, 0);
		
	}
}
