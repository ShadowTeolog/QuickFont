using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using OpenTK;
using OpenTK.Graphics.ES20;

namespace QuickFont
{
    public class QVertexArrayObject : IDisposable
    {
        
        private int _vertexCount;  //current collected vertex count in buffer
        private int _vertexCapacity;
        private int _vboDataCapacity;
        private int _vboid;
        private readonly SharedState _qFontSharedState;
        private QVertex[] _vertexArray;
        private static readonly int QVertexStride;

        static QVertexArrayObject()
        {
            QVertexStride = BlittableValueType.StrideOf(default(QVertex));
        }

        public QVertexArrayObject(SharedState state)
        {
            _qFontSharedState = state;
            _vertexCapacity = 1000;
            _vertexArray = new QVertex[_vertexCapacity];
            _vboDataCapacity = 0;

            GL.UseProgram(_qFontSharedState.ShaderVariables.ShaderProgram);
            GL.GenBuffers(1, out _vboid);
        }

        private void AllocSpace(int count)
        {
            if (_vertexCapacity < count)
            {
                while(_vertexCapacity < count)
                    _vertexCapacity *= 2;

                Array.Resize(ref _vertexArray,_vertexCapacity);
            }
        }
        internal void AddVertexes(IList<QVertex> vertices)
        {
            if (vertices.Count != 0)
            {
                AllocSpace(vertices.Count + _vertexCount);
                vertices.CopyTo(_vertexArray, _vertexCount);
                _vertexCount += vertices.Count;
            }
        }

        internal void AddVertex(Vector3 position, Vector2 textureCoord, Vector4 colour)
        {
            AllocSpace(_vertexCount+1);
            _vertexArray[_vertexCount++] = new QVertex
            {
                Position = position,
                TextureCoord = textureCoord,
                VertexColor = colour
            };
        }

        public void Load()
        {
            if (_vertexCount == 0)
                return;
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vboid);
            EnableAttributes();

            var spaceneeded = _vertexCount * QVertexStride;
            if (spaceneeded > _vboDataCapacity) //if no space then update with reallocation
            {
                _vboDataCapacity = spaceneeded;
                GL.BufferData(BufferTarget.ArrayBuffer, spaceneeded, _vertexArray, BufferUsageHint.StreamDraw);
            }
            else
                GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, spaceneeded, _vertexArray);
        }

        public void Reset()
        {
            _vertexCount = 0;
        }

        public void Bind()
        {
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vboid);
            EnableAttributes();
        }

        public void Dispose()
        {
            GL.DeleteBuffers(1, ref _vboid);
        }

        public void DisableAttributes()
        {
            GL.DisableVertexAttribArray(_qFontSharedState.ShaderVariables.PositionCoordAttribLocation);
            GL.DisableVertexAttribArray(_qFontSharedState.ShaderVariables.TextureCoordAttribLocation);
            GL.DisableVertexAttribArray(_qFontSharedState.ShaderVariables.ColorCoordAttribLocation);
        }

        private void EnableAttributes()
        {
            int stride = QVertexStride;
            GL.EnableVertexAttribArray(_qFontSharedState.ShaderVariables.PositionCoordAttribLocation);
            GL.EnableVertexAttribArray(_qFontSharedState.ShaderVariables.TextureCoordAttribLocation);
            GL.EnableVertexAttribArray(_qFontSharedState.ShaderVariables.ColorCoordAttribLocation);
            GL.VertexAttribPointer(_qFontSharedState.ShaderVariables.PositionCoordAttribLocation, 3,
                VertexAttribPointerType.Float, false, stride, IntPtr.Zero);
            GL.VertexAttribPointer(_qFontSharedState.ShaderVariables.TextureCoordAttribLocation, 2, VertexAttribPointerType.Float,
                false,
                stride, new IntPtr(3*sizeof (float)));
            GL.VertexAttribPointer(_qFontSharedState.ShaderVariables.ColorCoordAttribLocation, 4, VertexAttribPointerType.Float,
                false, stride, new IntPtr(5*sizeof (float)));
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct QVertex
    {
        public Vector3 Position;
        public Vector2 TextureCoord;
        public Vector4 VertexColor;
    }
}
