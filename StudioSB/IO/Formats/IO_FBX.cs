﻿using System;
using System.Collections.Generic;
using StudioSB.IO.Models;
using StudioSB.Scenes;
using Fbx.ForgeFBX;
using OpenTK;

namespace StudioSB.IO.Formats
{
    //TODO: only works with version 7.4
    public class IO_FBX : IImportableModelType
    {
        public string Name => "FBX";

        public string Extension => ".fbx";

        public IOModel ImportIOModel(string FileName)
        {
            IOModel model = new IOModel();
            SBSkeleton skeleton = new SBSkeleton();
            model.Skeleton = skeleton;

            var test = Fbx.FbxIO.ReadBinary(FileName);
            if(test.Version != Fbx.FbxVersion.v7_4)
            {
                throw new NotSupportedException($"Only FBX version 7.4 is currently supported: Imported version = {test.Version}");
            }

            FbxAccessor accessor = new FbxAccessor(FileName);

            //Bones
            var limbs = accessor.GetLimbNodes();

            foreach(var limb in limbs)
            {
                skeleton.AddRoot(ConvertLimbToSBBone(limb));
            }

            // Fast access to bone indices
            Dictionary<string, int> BoneNameToIndex = new Dictionary<string, int>();
            foreach(var b in skeleton.Bones)
            {
                BoneNameToIndex.Add(b.Name, skeleton.IndexOfBone(b));
            }

            // Mesh

            var models = accessor.GetModels();

            int YupAxis = accessor.GetOriginalXAxis();
            foreach (var mod in models)
            {
                // rotation 90
                Matrix4 transform = (YupAxis == 2 ? Matrix4.CreateRotationX(-90 * DegToRag) : Matrix4.Identity) * GetModelTransform(mod);

                foreach (var geom in mod.Geometries)
                {
                    IOMesh mesh = new IOMesh();
                    mesh.Name = geom.Name;
                    model.Meshes.Add(mesh);

                    // Create Rigging information
                    Vector4[] BoneIndices = new Vector4[geom.Vertices.Length];
                    Vector4[] BoneWeights = new Vector4[geom.Vertices.Length];
                    foreach (var deformer in geom.Deformers)
                    {
                        //TODO: this shouldn't happen...
                        if (!BoneNameToIndex.ContainsKey(deformer.Name))
                            continue;
                        int index = BoneNameToIndex[deformer.Name];

                        for (int i = 0; i < deformer.Indices.Length; i++)
                        {
                            int vertexIndex = deformer.Indices[i];
                            for (int j = 0; j < 4; j++)
                            {
                                if (BoneWeights[vertexIndex][j] == 0)
                                {
                                    BoneIndices[vertexIndex][j] = index;
                                    BoneWeights[vertexIndex][j] = (float)deformer.Weights[i];
                                    break;
                                }
                            }
                        }
                        //SBConsole.WriteLine(deformer.Name + " " + deformer.Weights.Length + " " + deformer.Indices.Length + " " + index);
                    }

                    // Explanation:
                    // negative values are used to indicate a stopping point for the face
                    // so every 3rd index needed to be adjusted
                    for (int i = 0; i < geom.Indices.Length; i += 3)
                    {
                        mesh.Indices.Add((uint)i);
                        mesh.Indices.Add((uint)i + 1);
                        mesh.Indices.Add((uint)i + 2);
                        mesh.Vertices.Add(CreateVertex(transform, geom, i, BoneIndices, BoneWeights));
                        mesh.Vertices.Add(CreateVertex(transform, geom, i + 1, BoneIndices, BoneWeights));
                        mesh.Vertices.Add(CreateVertex(transform, geom, i + 2, BoneIndices, BoneWeights));
                    }

                    mesh.HasPositions = true;

                    //SBConsole.WriteLine(geom.Vertices.Length);
                    foreach (var layer in geom.Layers)
                    {
                        switch (layer.Name)
                        {
                            case "LayerElementNormal":
                            case "LayerElementUV":
                            case "LayerElementColor":
                                break;
                            default:
                                SBConsole.WriteLine(layer.Name + " " + layer.ReferenceInformationType + " " + layer.Data.Length + " " + (layer.ReferenceInformationType.Equals("IndexToDirect") ? layer.Indices.Length.ToString() : ""));
                                break;
                        }
                    }
                }
            }
            

            //PostProcessNormals(model);

            return model;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="geometry"></param>
        /// <param name="Index"></param>
        /// <returns></returns>
        private static IOVertex CreateVertex(Matrix4 transform, FbxGeometry geometry, int Index, Vector4[] boneIndices, Vector4[] boneWeights)
        {
            int VertexIndex = geometry.Indices[Index];
            if((Index + 1) % 3 == 0)
                VertexIndex = (geometry.Indices[Index] + 1) * -1;

            IOVertex vertex = new IOVertex(){
                Position = Vector3.TransformPosition(
                    new Vector3(
                        (float)geometry.Vertices[VertexIndex * 3],
                        (float)geometry.Vertices[VertexIndex * 3 + 1], 
                        (float)geometry.Vertices[VertexIndex * 3 + 2]), 
                    transform),
                BoneIndices = boneIndices[VertexIndex],
                BoneWeights = boneWeights[VertexIndex]
            };

            //Deforming
            
            foreach (var layer in geometry.Layers)
            {
                int layerIndex = Index;
                if (!layer.ReferenceInformationType.Equals("Direct"))
                {
                    layerIndex = layer.Indices[Index];
                }
                switch (layer.Name)
                {
                    case "LayerElementNormal":
                        vertex.Normal = new Vector3((float)layer.Data[layerIndex * 3], (float)layer.Data[layerIndex * 3 + 1], (float)layer.Data[layerIndex * 3 + 2]);
                        vertex.Normal = Vector3.TransformNormal(vertex.Normal, transform);
                        break;
                    case "LayerElementColor":
                        vertex.Color = new Vector4((float)layer.Data[layerIndex * 4], (float)layer.Data[layerIndex * 4 + 1], (float)layer.Data[layerIndex * 4 + 2], (float)layer.Data[layerIndex * 4 + 3]);
                        break;
                    case "LayerElementUV":
                        Vector2 uv = new Vector2((float)layer.Data[layerIndex * 2], (float)layer.Data[layerIndex * 2 + 1]);
                        if (layer.Layer == 0)
                            vertex.UV0 = uv;
                        if (layer.Layer == 1)
                            vertex.UV1 = uv;
                        break;
                }
            }

            return vertex;
        }

        private static float DegToRag = (float)Math.PI / 180;

        /// <summary>
        /// Gets matrix4 transform for model node
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        private static Matrix4 GetModelTransform(FbxModel model)
        {
            SBBone bone = new SBBone();

            bone.Transform = Matrix4.Identity;
            
            bone.Translation = new Vector3((float)model.LclTranslation.X, (float)model.LclTranslation.Y, (float)model.LclTranslation.Z);
            bone.RotationEuler = new Vector3((float)model.LclRotation.X * DegToRag, (float)model.LclRotation.Y * DegToRag, (float)model.LclRotation.Z * DegToRag);
            bone.Scale = new Vector3((float)model.LclScaling.X, (float)model.LclScaling.Y, (float)model.LclScaling.Z);

            return bone.Transform;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="limb"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        private static SBBone ConvertLimbToSBBone(FbxLimbNode limb, SBBone parent = null)
        {
            SBBone bone = new SBBone();
            bone.Name = limb.Name;

            bone.Transform = Matrix4.Identity;

            //who needs precision anyways
            bone.Translation = new Vector3((float)limb.LclTranslation.X, (float)limb.LclTranslation.Y, (float)limb.LclTranslation.Z);
            bone.RotationEuler = new Vector3((float)limb.LclRotation.X*DegToRag, (float)limb.LclRotation.Y * DegToRag, (float)limb.LclRotation.Z * DegToRag);
            bone.Scale = new Vector3((float)limb.LclScaling.X, (float)limb.LclScaling.Y, (float)limb.LclScaling.Z);

            if (parent != null)
                bone.Parent = parent;

            //process children
            foreach(var child in limb.Children)
            {
                ConvertLimbToSBBone(child, bone);
            }

            return bone;
        }

        /// <summary>
        /// Does some smoothing on normals to fix seams
        /// </summary>
        private void PostProcessNormals(IOModel model)
        {
            float e = 0.0001f;

            Dictionary<Vector3, IOVertex> positionToVertex = new Dictionary<Vector3, IOVertex>();
            Dictionary<IOVertex, List<IOVertex>> VertexGroups = new Dictionary<IOVertex, List<IOVertex>>();

            foreach(var mesh in model.Meshes)
            {
                foreach(var vert in mesh.Vertices)
                {
                    bool matched = false;
                    // perfect match
                    if(positionToVertex.ContainsKey(vert.Position))
                    {
                        matched = true;
                        if (!VertexGroups.ContainsKey(positionToVertex[vert.Position]))
                        {
                            VertexGroups.Add(positionToVertex[vert.Position], new List<IOVertex>());
                            VertexGroups[positionToVertex[vert.Position]].Add(positionToVertex[vert.Position]);
                        }
                        VertexGroups[positionToVertex[vert.Position]].Add(vert);
                    }

                    // distance less than epsilon
                    /*if (!matched)
                        foreach (var pos in positionToVertex.Keys)
                        {
                            if ((pos.X - vert.Position.X) * (pos.X - vert.Position.X)
                                + (pos.Y - vert.Position.Y) * (pos.Y - vert.Position.Y)
                                + (pos.Z - vert.Position.Z) * (pos.Z - vert.Position.Z)
                                < e * e)
                            {
                                matched = true;
                                if (!VertexGroups.ContainsKey(positionToVertex[pos]))
                                {
                                    VertexGroups.Add(positionToVertex[pos], new List<IOVertex>());
                                    VertexGroups[positionToVertex[pos]].Add(positionToVertex[pos]);
                                }
                                VertexGroups[positionToVertex[pos]].Add(vert);
                                break;
                            }
                        }*/

                    // new position
                    if(!matched)
                        positionToVertex.Add(vert.Position, vert);
                }
            }

            SBConsole.WriteLine("Smoothing groups " + VertexGroups.Count);
            foreach(var v in VertexGroups)
            {
                Vector3 newNormal = Vector3.Zero;
                foreach(var normal in v.Value)
                {
                    newNormal += normal.Normal;
                }
                newNormal = newNormal / v.Value.Count;
                newNormal.Normalize();
                foreach (var normal in v.Value)
                {
                    normal.Normal = newNormal;
                }
            }
        }

    }
}