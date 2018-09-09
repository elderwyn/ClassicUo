﻿#region license
//  Copyright (C) 2018 ClassicUO Development Community on Github
//
//	This project is an alternative client for the game Ultima Online.
//	The goal of this is to develop a lightweight client considering 
//	new technologies.  
//  (Copyright (c) 2018 ClassicUO Development Team)
//    
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <https://www.gnu.org/licenses/>.
#endregion
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ClassicUO.Game.Renderer
{
    // https://git.gmantaos.com/gamedev/Engine/blob/fcc0d5bcca1e9fcf5eb8481185ff7ecffbbe4fe2/Engine/Nez/Utils/MonoGameCompat.cs

    public static class Ext
    {
        public static void DrawIndexedPrimitives(this GraphicsDevice self, PrimitiveType primitiveType, int baseVertex, int startIndex, int primitiveCount)
        {
            self.DrawIndexedPrimitives(primitiveType, baseVertex, 0, primitiveCount * 2, startIndex, primitiveCount);
        }
    }
    public class SpriteBatch3D
    {
        private const int MAX_VERTICES_PER_DRAW = 0x8000;
        private const int INITIAL_TEXTURE_COUNT = 0x800;
        private const float MAX_ACCURATE_SINGLE_FLOAT = 65536;

        private readonly DepthStencilState _dss = new DepthStencilState { DepthBufferEnable = true, DepthBufferWriteEnable = true };
        private readonly Microsoft.Xna.Framework.Game _game;
        private readonly short[] _indices = new short[MAX_VERTICES_PER_DRAW * 6];
        private readonly short[] _sortedIndices = new short[MAX_VERTICES_PER_DRAW * 6];

        private readonly Vector3 _minVector3 = new Vector3(0, 0, int.MinValue);
        private readonly SpriteVertex[] _vertices = new SpriteVertex[MAX_VERTICES_PER_DRAW];

        private readonly EffectTechnique _huesTechnique;
        private readonly Effect _effect;
        private readonly EffectParameter _viewportEffect;
        private readonly EffectParameter _worldMatrixEffect;
        private readonly EffectParameter _projectionMatrixEffect;
        private readonly EffectParameter _drawLightingEffect;

        private BoundingBox _drawingArea;
        private float _z;
        private readonly DrawCallInfo[] _drawCalls;


        private int _enqueuedDrawCalls = 0;
        private int _vertexCount = 0;
        private int _indicesCount = 0;
        private DynamicVertexBuffer _vertexBuffer;
        private DynamicIndexBuffer _indexBuffer;


        public SpriteBatch3D(Microsoft.Xna.Framework.Game game)
        {
            _game = game;

            //for (int i = 0; i < MAX_VERTICES_PER_DRAW; i++)
            //{
            //    _indices[i * 6] = (short)(i * 4);
            //    _indices[i * 6 + 1] = (short)(i * 4 + 1);
            //    _indices[i * 6 + 2] = (short)(i * 4 + 2);
            //    _indices[i * 6 + 3] = (short)(i * 4 + 2);
            //    _indices[i * 6 + 4] = (short)(i * 4 + 1);
            //    _indices[i * 6 + 5] = (short)(i * 4 + 3);
            //}

            _effect = new Effect(GraphicsDevice, File.ReadAllBytes(Path.Combine(Environment.CurrentDirectory, "Graphic/Shaders/IsometricWorld.fxc")));

            _effect.Parameters["HuesPerTexture"].SetValue(/*IO.Resources.Hues.HuesCount*/3000f);

            _drawLightingEffect = _effect.Parameters["DrawLighting"];
            _projectionMatrixEffect = _effect.Parameters["ProjectionMatrix"];
            _worldMatrixEffect = _effect.Parameters["WorldMatrix"];
            _viewportEffect = _effect.Parameters["Viewport"];

            _huesTechnique = _effect.Techniques["HueTechnique"];


            _drawCalls = new DrawCallInfo[MAX_VERTICES_PER_DRAW];

            _vertexBuffer = new DynamicVertexBuffer(GraphicsDevice, SpriteVertex.VertexDeclaration, _vertices.Length, BufferUsage.WriteOnly);
            _indexBuffer = new DynamicIndexBuffer(GraphicsDevice, IndexElementSize.SixteenBits, _indices.Length, BufferUsage.WriteOnly);

        }


        public GraphicsDevice GraphicsDevice => _game?.GraphicsDevice;
        public Matrix ProjectionMatrixWorld => Matrix.Identity;
        public Matrix ProjectionMatrixScreen => Matrix.CreateOrthographicOffCenter(0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height, 0f, short.MinValue, short.MaxValue);

        private bool _isStarted;

        public void SetLightDirection(Vector3 dir)
        {
            _effect.Parameters["lightDirection"].SetValue(dir);
        }

        public void SetLightIntensity(float inte)
        {
            _effect.Parameters["lightIntensity"].SetValue(inte);
        }

        public float GetZ() => _z++;

        public void BeginDraw()
        {
            if (_isStarted)
                throw new Exception("");

            _isStarted = true;

            _enqueuedDrawCalls = 0;
            TotalMergin = 0;

            _z = 0;
            _drawingArea.Min = _minVector3;
            _drawingArea.Max = new Vector3(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height, int.MaxValue);
        }

        public bool DrawSprite(Texture2D texture, SpriteVertex[] vertices)
        {
            if (!_isStarted)
                throw new Exception();

            if (texture == null || texture.IsDisposed)
            {
                return false;
            }

            bool draw = false;

            for (byte i = 0; i < 4; i++)
            {
                if (_drawingArea.Contains(vertices[i].Position) == ContainmentType.Contains)
                {
                    draw = true;
                    break;
                }
            }

            if (!draw)
                return false;

            const int VERTEX_COUNT = 4;
            const int INDEX_COUNT = 6;
            const int PRIMITIVES_COUNT = 2;

            vertices[0].Position.Z = vertices[1].Position.Z = vertices[2].Position.Z = vertices[3].Position.Z = GetZ();


            AddQuadrilateralIndices(_vertexCount);

            var call = new DrawCallInfo(texture, _indicesCount, PRIMITIVES_COUNT);


            Array.Copy(vertices, 0, _vertices, _vertexCount, VERTEX_COUNT);
            _vertexCount += VERTEX_COUNT;
            Array.Copy(_geometryIndices, 0, _indices, _indicesCount, INDEX_COUNT);
            _indicesCount += INDEX_COUNT;

            Enqueue(ref call);

            return true;
        }

        private readonly short[] _geometryIndices = new short[6];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddQuadrilateralIndices(int indexOffset)
        {
            _geometryIndices[0] = (short)(0 + indexOffset);
            _geometryIndices[1] = (short)(1 + indexOffset);
            _geometryIndices[2] = (short)(2 + indexOffset);
            _geometryIndices[3] = (short)(1 + indexOffset);
            _geometryIndices[4] = (short)(3 + indexOffset);
            _geometryIndices[5] = (short)(2 + indexOffset);
        }

        public void EndDraw(bool light = false)
        {
            if (!_isStarted)
                throw new Exception();
            if (_enqueuedDrawCalls == 0)
                return;

            _vertexBuffer.SetData(_vertices, 0, _vertexCount);
            GraphicsDevice.SetVertexBuffer(_vertexBuffer);


            //Array.Sort(_drawCalls, 0, _enqueuedDrawCalls);

            //int newDrawCallCount = 0;
            //_drawCalls[0].StartIndex = 0;
            //var currentDrawCall = _drawCalls[0];
            //_drawCalls[newDrawCallCount++] = _drawCalls[0];

            //int drawCallIndexCount = currentDrawCall.PrimitiveCount * 3;
            //Array.Copy(_indices, currentDrawCall.StartIndex, _sortedIndices, 0, drawCallIndexCount);
            //int sortedIndexCount = drawCallIndexCount;

            //for (int i = 1; i < _enqueuedDrawCalls; i++)
            //{
            //    currentDrawCall = _drawCalls[i];

            //    drawCallIndexCount = currentDrawCall.PrimitiveCount * 3;
            //    Array.Copy(_indices, currentDrawCall.StartIndex, _sortedIndices, sortedIndexCount, drawCallIndexCount);
            //    sortedIndexCount += drawCallIndexCount;

            //    if (currentDrawCall.TryMerge(ref _drawCalls[newDrawCallCount - 1]))
            //    {
            //        TotalMergin++;
            //        continue;
            //    }

            //    currentDrawCall.StartIndex = sortedIndexCount - drawCallIndexCount;
            //    _drawCalls[newDrawCallCount++] = currentDrawCall;
            //}

            //_enqueuedDrawCalls = newDrawCallCount;


            _indexBuffer.SetData(_indices, 0, _indicesCount);
            GraphicsDevice.Indices = _indexBuffer;

            _indicesCount = 0;
            _vertexCount = 0;

            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.SamplerStates[0] = SamplerState.PointClamp;
            GraphicsDevice.SamplerStates[1] = SamplerState.PointClamp;

            //GraphicsDevice.SamplerStates[2] = SamplerState.PointClamp;
            //GraphicsDevice.SamplerStates[3] = SamplerState.PointClamp;

            //GraphicsDevice.SamplerStates[4] = SamplerState.PointWrap;

            _drawLightingEffect.SetValue(light);
            // set up viewport.
            _projectionMatrixEffect.SetValue(ProjectionMatrixScreen);
            _worldMatrixEffect.SetValue(ProjectionMatrixWorld);
            _viewportEffect.SetValue(new Vector2(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height));

            GraphicsDevice.DepthStencilState = _dss;


            _effect.CurrentTechnique = _huesTechnique;
            _effect.CurrentTechnique.Passes[0].Apply();

            for (int i = 0; i < _enqueuedDrawCalls; i++)
            {
                ref var call = ref _drawCalls[i];

                GraphicsDevice.Textures[0] = call.Texture;
                GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, call.StartIndex, call.PrimitiveCount);

                //GraphicsDevice.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _vertices, 0, _vertices.Length, _sortedIndices, call.StartIndex, call.PrimitiveCount);
            }

            Array.Clear(_drawCalls, 0, _enqueuedDrawCalls);

            _isStarted = false;
        }

        private void Enqueue(ref DrawCallInfo call)
        {
            if (_enqueuedDrawCalls > 0 && call.TryMerge(ref _drawCalls[_enqueuedDrawCalls - 1]))
            {
                TotalMergin++;
                return;
            }
            _drawCalls[_enqueuedDrawCalls++] = call;
        }

        public int TotalMergin { get; private set; }
        public int TotalCalls => _enqueuedDrawCalls;




        struct DrawCallInfo : IComparable<DrawCallInfo>
        {
            public DrawCallInfo(Texture2D texture, int start, int count)
            {
                Texture = texture;
                TextureKey = (uint)RuntimeHelpers.GetHashCode(texture);
                StartIndex = start;
                PrimitiveCount = count;
            }

            public readonly Texture2D Texture;
            public readonly uint TextureKey;
            public int StartIndex;
            public int PrimitiveCount;

            public bool TryMerge(ref DrawCallInfo callInfo)
            {
                if (TextureKey != callInfo.TextureKey)
                    return false;
                callInfo.PrimitiveCount += PrimitiveCount;
                return true;
            }

            public int CompareTo(DrawCallInfo other)
            {
                return other.TextureKey.CompareTo(TextureKey);
            }
        }

    }
    //public class SpriteBatch3D
    //{
    //    private const int MAX_VERTICES_PER_DRAW = 0x8000;
    //    private const int INITIAL_TEXTURE_COUNT = 0x800;
    //    private const float MAX_ACCURATE_SINGLE_FLOAT = 65536;

    //    private readonly DepthStencilState _dss = new DepthStencilState { DepthBufferEnable = true, DepthBufferWriteEnable = true };
    //    private readonly Microsoft.Xna.Framework.Game _game;
    //    private readonly short[] _indices = new short[MAX_VERTICES_PER_DRAW * 6];
    //    private readonly short[] _sortedIndices = new short[MAX_VERTICES_PER_DRAW * 6];

    //    private readonly Vector3 _minVector3 = new Vector3(0, 0, int.MinValue);
    //    private readonly SpriteVertex[] _vertices = new SpriteVertex[MAX_VERTICES_PER_DRAW];

    //    private readonly EffectTechnique _huesTechnique;
    //    private readonly Effect _effect;
    //    private readonly EffectParameter _viewportEffect;
    //    private readonly EffectParameter _worldMatrixEffect;
    //    private readonly EffectParameter _projectionMatrixEffect;
    //    private readonly EffectParameter _drawLightingEffect;

    //    private BoundingBox _drawingArea;
    //    private float _z;
    //    private readonly DrawCallInfo[] _drawCalls;


    //    private int _enqueuedDrawCalls = 0;
    //    private int _vertexCount = 0;
    //    private int _indicesCount = 0;
    //    private DynamicVertexBuffer _vertexBuffer;
    //    private DynamicIndexBuffer _indexBuffer;


    //    public SpriteBatch3D(Microsoft.Xna.Framework.Game game)
    //    {
    //        _game = game;

    //        //for (int i = 0; i < MAX_VERTICES_PER_DRAW; i++)
    //        //{
    //        //    _indices[i * 6] = (short)(i * 4);
    //        //    _indices[i * 6 + 1] = (short)(i * 4 + 1);
    //        //    _indices[i * 6 + 2] = (short)(i * 4 + 2);
    //        //    _indices[i * 6 + 3] = (short)(i * 4 + 2);
    //        //    _indices[i * 6 + 4] = (short)(i * 4 + 1);
    //        //    _indices[i * 6 + 5] = (short)(i * 4 + 3);
    //        //}

    //        _effect = new Effect(GraphicsDevice, File.ReadAllBytes(Path.Combine(Environment.CurrentDirectory, "Graphic/Shaders/IsometricWorld.fxc")));

    //        _effect.Parameters["HuesPerTexture"].SetValue(/*IO.Resources.Hues.HuesCount*/3000f);

    //        _drawLightingEffect = _effect.Parameters["DrawLighting"];
    //        _projectionMatrixEffect = _effect.Parameters["ProjectionMatrix"];
    //        _worldMatrixEffect = _effect.Parameters["WorldMatrix"];
    //        _viewportEffect = _effect.Parameters["Viewport"];

    //        _huesTechnique = _effect.Techniques["HueTechnique"];


    //        _drawCalls = new DrawCallInfo[MAX_VERTICES_PER_DRAW];

    //        _vertexBuffer = new DynamicVertexBuffer(GraphicsDevice, SpriteVertex.VertexDeclaration, _vertices.Length, BufferUsage.WriteOnly);
    //        _indexBuffer = new DynamicIndexBuffer(GraphicsDevice, IndexElementSize.SixteenBits, _indices.Length, BufferUsage.WriteOnly);

    //    }


    //    public GraphicsDevice GraphicsDevice => _game?.GraphicsDevice;
    //    public Matrix ProjectionMatrixWorld => Matrix.Identity;
    //    public Matrix ProjectionMatrixScreen => Matrix.CreateOrthographicOffCenter(0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height, 0f, short.MinValue, short.MaxValue);


    //    public void SetLightDirection(Vector3 dir)
    //    {
    //        _effect.Parameters["lightDirection"].SetValue(dir);
    //    }

    //    public void SetLightIntensity(float inte)
    //    {
    //        _effect.Parameters["lightIntensity"].SetValue(inte);
    //    }

    //    public float GetZ() => _z++;

    //    public void BeginDraw()
    //    {
    //        _z = 0;
    //        _drawingArea.Min = _minVector3;
    //        _drawingArea.Max = new Vector3(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height, int.MaxValue);
    //    }

    //    public bool DrawSprite(Texture2D texture, SpriteVertex[] vertices)
    //    {
    //        if (texture == null || texture.IsDisposed)
    //        {
    //            return false;
    //        }

    //        bool draw = false;

    //        for (byte i = 0; i < 4; i++)
    //        {
    //            if (_drawingArea.Contains(vertices[i].Position) == ContainmentType.Contains)
    //            {
    //                draw = true;
    //                break;
    //            }
    //        }

    //        if (!draw)
    //            return false;

    //        const int VERTEX_COUNT = 4;
    //        const int INDEX_COUNT = 6;
    //        const int PRIMITIVES_COUNT = 2;

    //        vertices[0].Position.Z = vertices[1].Position.Z = vertices[2].Position.Z = vertices[3].Position.Z = GetZ();


    //        AddQuadrilateralIndices(_vertexCount);

    //        var call = new DrawCallInfo(texture, _indicesCount, PRIMITIVES_COUNT);


    //        Array.Copy(vertices, 0, _vertices, _vertexCount, VERTEX_COUNT);
    //        _vertexCount += VERTEX_COUNT;
    //        Array.Copy(_geometryIndices, 0, _indices, _indicesCount, INDEX_COUNT);
    //        _indicesCount += INDEX_COUNT;

    //        Enqueue(ref call);

    //        return true;
    //    }

    //    private readonly short[] _geometryIndices = new short[6];

    //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //    private void AddQuadrilateralIndices(int indexOffset)
    //    {
    //        _geometryIndices[0] = (short)(0 + indexOffset);
    //        _geometryIndices[1] = (short)(1 + indexOffset);
    //        _geometryIndices[2] = (short)(2 + indexOffset);
    //        _geometryIndices[3] = (short)(1 + indexOffset);
    //        _geometryIndices[4] = (short)(3 + indexOffset);
    //        _geometryIndices[5] = (short)(2 + indexOffset);
    //    }

    //    public void EndDraw(bool light = false)
    //    {
    //        if (_enqueuedDrawCalls == 0)
    //            return;

    //        _vertexBuffer.SetData(_vertices, 0, _vertexCount);
    //        GraphicsDevice.SetVertexBuffer(_vertexBuffer);


    //        Array.Sort(_drawCalls, 0, _enqueuedDrawCalls);

    //        int newDrawCallCount = 0;
    //        _drawCalls[0].StartIndex = 0;
    //        var currentDrawCall = _drawCalls[0];
    //        _drawCalls[newDrawCallCount++] = _drawCalls[0];

    //        int drawCallIndexCount = currentDrawCall.PrimitiveCount * 3;
    //        Array.Copy(_indices, currentDrawCall.StartIndex, _sortedIndices, 0, drawCallIndexCount);
    //        int sortedIndexCount = drawCallIndexCount;

    //        for (int i = 1; i < _enqueuedDrawCalls; i++)
    //        {
    //            currentDrawCall = _drawCalls[i];

    //            drawCallIndexCount = currentDrawCall.PrimitiveCount * 3;
    //            Array.Copy(_indices, currentDrawCall.StartIndex, _sortedIndices, sortedIndexCount, drawCallIndexCount);
    //            sortedIndexCount += drawCallIndexCount;

    //            if (currentDrawCall.TryMerge(ref _drawCalls[newDrawCallCount - 1]))
    //                continue;

    //            currentDrawCall.StartIndex = sortedIndexCount - drawCallIndexCount;
    //            _drawCalls[newDrawCallCount++] = currentDrawCall;
    //        }

    //        _enqueuedDrawCalls = newDrawCallCount;


    //        _indexBuffer.SetData(_sortedIndices, 0, _indicesCount);
    //        GraphicsDevice.Indices = _indexBuffer;

    //        _indicesCount = 0;
    //        _vertexCount = 0;

    //        GraphicsDevice.BlendState = BlendState.AlphaBlend;
    //        GraphicsDevice.RasterizerState = RasterizerState.CullNone;
    //        GraphicsDevice.SamplerStates[0] = SamplerState.PointClamp;
    //        GraphicsDevice.SamplerStates[1] = SamplerState.PointClamp;

    //        //GraphicsDevice.SamplerStates[2] = SamplerState.PointClamp;
    //        //GraphicsDevice.SamplerStates[3] = SamplerState.PointClamp;

    //        //GraphicsDevice.SamplerStates[4] = SamplerState.PointWrap;

    //        _drawLightingEffect.SetValue(light);
    //        // set up viewport.
    //        _projectionMatrixEffect.SetValue(ProjectionMatrixScreen);
    //        _worldMatrixEffect.SetValue(ProjectionMatrixWorld);
    //        _viewportEffect.SetValue(new Vector2(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height));

    //        GraphicsDevice.DepthStencilState = _dss;


    //        _effect.CurrentTechnique = _huesTechnique;
    //        _effect.CurrentTechnique.Passes[0].Apply();

    //        for (int i = 0; i < _enqueuedDrawCalls; i++)
    //        {
    //            ref var call = ref _drawCalls[i];

    //            GraphicsDevice.Textures[0] = call.Texture;
    //            GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, call.StartIndex, call.PrimitiveCount);

    //            //GraphicsDevice.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _vertices, 0, _vertices.Length, _sortedIndices, call.StartIndex, call.PrimitiveCount);
    //        }

    //        Array.Clear(_drawCalls, 0, _enqueuedDrawCalls);
    //        _enqueuedDrawCalls = 0;
    //    }

    //    private void Enqueue(ref DrawCallInfo call)
    //    {
    //        if (_enqueuedDrawCalls > 0 && call.TryMerge(ref _drawCalls[_enqueuedDrawCalls - 1]))
    //            return;

    //        _drawCalls[_enqueuedDrawCalls++] = call;
    //    }






    //    struct DrawCallInfo : IComparable<DrawCallInfo>
    //    {
    //        public DrawCallInfo(Texture2D texture, int start, int count)
    //        {
    //            Texture = texture;
    //            TextureKey = (uint)RuntimeHelpers.GetHashCode(texture);
    //            StartIndex = start;
    //            PrimitiveCount = count;
    //        }

    //        public readonly Texture2D Texture;
    //        public readonly uint TextureKey;
    //        public int StartIndex;
    //        public int PrimitiveCount;

    //        public bool TryMerge(ref DrawCallInfo callInfo)
    //        {
    //            if (TextureKey != callInfo.TextureKey)
    //                return false;
    //            callInfo.PrimitiveCount += PrimitiveCount;
    //            return true;
    //        }

    //        public int CompareTo(DrawCallInfo other)
    //        {
    //            return other.TextureKey.CompareTo(TextureKey);
    //        }
    //    }

    //}
}