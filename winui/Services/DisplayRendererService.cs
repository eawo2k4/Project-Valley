using System;
using System.Runtime.InteropServices;

namespace Box86_WinUI.Services
{
    [ComImport]
    [Guid("63aad0b8-7c24-40ff-85a8-640d944cc325")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ISwapChainPanelNative
    {
        [PreserveSig]
        int SetSwapChain(IntPtr swapChain);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DXGI_SAMPLE_DESC
    {
        public uint Count;
        public uint Quality;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DXGI_SWAP_CHAIN_DESC1
    {
        public uint Width;
        public uint Height;
        public uint Format;
        public int Stereo;
        public DXGI_SAMPLE_DESC SampleDesc;
        public uint BufferUsage;
        public uint BufferCount;
        public uint Scaling;
        public uint SwapEffect;
        public uint AlphaMode;
        public uint Flags;
    }

    [ComImport]
    [Guid("50c83a1c-e072-4c48-87b0-3630fa36a6d0")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDXGIFactory2
    {
        [PreserveSig] int SetPrivateData(ref Guid Name, uint DataSize, IntPtr pData);
        [PreserveSig] int SetPrivateDataInterface(ref Guid Name, IntPtr pUnknown);
        [PreserveSig] int GetPrivateData(ref Guid Name, ref uint pDataSize, IntPtr pData);
        [PreserveSig] int GetParent(ref Guid riid, out IntPtr ppParent);
        [PreserveSig] int EnumAdapters(uint Adapter, out IntPtr ppAdapter);
        [PreserveSig] int MakeWindowAssociation(IntPtr WindowHandle, uint Flags);
        [PreserveSig] int GetWindowAssociation(out IntPtr pWindowHandle);
        [PreserveSig] int CreateSwapChain(IntPtr pDevice, IntPtr pDesc, out IntPtr ppSwapChain);
        [PreserveSig] int CreateSoftwareAdapter(IntPtr Module, out IntPtr ppAdapter);
        [PreserveSig] int EnumAdapters1(uint Adapter, out IntPtr ppAdapter);
        [PreserveSig] int IsCurrent();
        [PreserveSig] int IsWindowedStereoEnabled();
        [PreserveSig] int CreateSwapChainForHwnd(IntPtr pDevice, IntPtr hWnd, ref DXGI_SWAP_CHAIN_DESC1 pDesc, IntPtr pFullscreenDesc, IntPtr pRestrictToOutput, out IntPtr ppSwapChain);
        [PreserveSig] int CreateSwapChainForCoreWindow(IntPtr pDevice, IntPtr pWindow, ref DXGI_SWAP_CHAIN_DESC1 pDesc, IntPtr pRestrictToOutput, out IntPtr ppSwapChain);
        [PreserveSig] int GetSharedResourceAdapterLuid(IntPtr hResource, out long pLuid);
        [PreserveSig] int RegisterStereoStatusWindow(IntPtr WindowHandle, uint wMsg, out uint pdwCookie);
        [PreserveSig] int RegisterStereoStatusEvent(IntPtr hEvent, out uint pdwCookie);
        [PreserveSig] void UnregisterStereoStatus(uint dwCookie);
        [PreserveSig] int RegisterOcclusionStatusWindow(IntPtr WindowHandle, uint wMsg, out uint pdwCookie);
        [PreserveSig] int RegisterOcclusionStatusEvent(IntPtr hEvent, out uint pdwCookie);
        [PreserveSig] void UnregisterOcclusionStatus(uint dwCookie);
        [PreserveSig] int CreateSwapChainForComposition(IntPtr pDevice, ref DXGI_SWAP_CHAIN_DESC1 pDesc, IntPtr pRestrictToOutput, out IntPtr ppSwapChain);
    }

    [ComImport]
    [Guid("790a45f8-f604-43cb-91a9-a42a8f140026")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDXGISwapChain1
    {
        [PreserveSig] int SetPrivateData(ref Guid Name, uint DataSize, IntPtr pData);
        [PreserveSig] int SetPrivateDataInterface(ref Guid Name, IntPtr pUnknown);
        [PreserveSig] int GetPrivateData(ref Guid Name, ref uint pDataSize, IntPtr pData);
        [PreserveSig] int GetParent(ref Guid riid, out IntPtr ppParent);
        [PreserveSig] int GetDevice(ref Guid riid, out IntPtr ppDevice);
        [PreserveSig] int Present(uint SyncInterval, uint Flags);
        [PreserveSig] int GetBuffer(uint Buffer, ref Guid riid, out IntPtr ppSurface);
        [PreserveSig] int SetFullscreenState(int Fullscreen, IntPtr pTarget);
        [PreserveSig] int GetFullscreenState(out int pFullscreen, out IntPtr ppTarget);
        [PreserveSig] int GetDesc(IntPtr pDesc);
        [PreserveSig] int ResizeBuffers(uint BufferCount, uint Width, uint Height, uint NewFormat, uint SwapChainFlags);
        [PreserveSig] int ResizeTarget(IntPtr pNewTargetParameters);
        [PreserveSig] int GetContainingOutput(out IntPtr ppOutput);
        [PreserveSig] int GetFrameStatistics(IntPtr pStats);
        [PreserveSig] int GetLastPresentCount(out uint pLastPresentCount);
        [PreserveSig] int GetDesc1(out DXGI_SWAP_CHAIN_DESC1 pDesc);
        [PreserveSig] int GetFullscreenDesc(IntPtr pDesc);
        [PreserveSig] int GetHwnd(out IntPtr pHwnd);
        [PreserveSig] int GetCoreWindow(ref Guid refiid, out IntPtr ppUnk);
        [PreserveSig] int Present1(uint SyncInterval, uint PresentFlags, IntPtr pPresentParameters);
    }

    [ComImport]
    [Guid("db6f6ddb-ac77-4e88-8253-819df9bbf140")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ID3D11Device
    {
        [PreserveSig] int CreateBuffer(IntPtr pDesc, IntPtr pInitialData, out IntPtr ppBuffer);
        [PreserveSig] int CreateTexture1D(IntPtr pDesc, IntPtr pInitialData, out IntPtr ppTexture1D);
        [PreserveSig] int CreateTexture2D(IntPtr pDesc, IntPtr pInitialData, out IntPtr ppTexture2D);
        [PreserveSig] int CreateTexture3D(IntPtr pDesc, IntPtr pInitialData, out IntPtr ppTexture3D);
        [PreserveSig] int CreateShaderResourceView(IntPtr pResource, IntPtr pDesc, out IntPtr ppSRView);
        [PreserveSig] int CreateUnorderedAccessView(IntPtr pResource, IntPtr pDesc, out IntPtr ppUAView);
        [PreserveSig] int CreateRenderTargetView(IntPtr pResource, IntPtr pDesc, out IntPtr ppRTView);
    }

    [ComImport]
    [Guid("c0b7ff06-4402-4945-88a6-7758d312fe20")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ID3D11DeviceContext
    {
        [PreserveSig] void VSSetConstantBuffers(uint StartSlot, uint NumBuffers, [In] IntPtr[] ppConstantBuffers);
        [PreserveSig] void PSSetShaderResources(uint StartSlot, uint NumViews, [In] IntPtr[] ppShaderResourceViews);
        [PreserveSig] void PSSetShader(IntPtr pPixelShader, IntPtr[] ppClassInstances, uint NumClassInstances);
        [PreserveSig] void PSSetSamplers(uint StartSlot, uint NumSamplers, [In] IntPtr[] ppSamplers);
        [PreserveSig] void VSSetShader(IntPtr pVertexShader, IntPtr[] ppClassInstances, uint NumClassInstances);
        [PreserveSig] void DrawIndexed(uint IndexCount, uint StartIndexLocation, int BaseVertexLocation);
        [PreserveSig] void Draw(uint VertexCount, uint StartVertexLocation);
        [PreserveSig] int Map(IntPtr pResource, uint Subresource, uint MapType, uint MapFlags, IntPtr pMappedResource);
        [PreserveSig] void Unmap(IntPtr pResource, uint Subresource);
        [PreserveSig] void PSSetConstantBuffers(uint StartSlot, uint NumBuffers, [In] IntPtr[] ppConstantBuffers);
        [PreserveSig] void IASetInputLayout(IntPtr pInputLayout);
        [PreserveSig] void IASetVertexBuffers(uint StartSlot, uint NumBuffers, [In] IntPtr[] ppVertexBuffers, [In] uint[] pStrides, [In] uint[] pOffsets);
        [PreserveSig] void IASetIndexBuffer(IntPtr pIndexBuffer, uint Format, uint Offset);
        [PreserveSig] void DrawIndexedInstanced(uint IndexCountPerInstance, uint InstanceCount, uint StartIndexLocation, int BaseVertexLocation, uint StartInstanceLocation);
        [PreserveSig] void DrawInstanced(uint VertexCountPerInstance, uint InstanceCount, uint StartVertexLocation, uint StartInstanceLocation);
        [PreserveSig] void GSSetConstantBuffers(uint StartSlot, uint NumBuffers, [In] IntPtr[] ppConstantBuffers);
        [PreserveSig] void GSSetShader(IntPtr pShader, IntPtr[] ppClassInstances, uint NumClassInstances);
        [PreserveSig] void IASetPrimitiveTopology(uint Topology);
        [PreserveSig] void VSSetShaderResources(uint StartSlot, uint NumViews, [In] IntPtr[] ppShaderResourceViews);
        [PreserveSig] void VSSetSamplers(uint StartSlot, uint NumSamplers, [In] IntPtr[] ppSamplers);
        [PreserveSig] void Begin(IntPtr pAsync);
        [PreserveSig] void End(IntPtr pAsync);
        [PreserveSig] int GetData(IntPtr pAsync, IntPtr pData, uint DataSize, uint GetDataFlags);
        [PreserveSig] void SetPredication(IntPtr pPredicate, int PredicateValue);
        [PreserveSig] void GSSetShaderResources(uint StartSlot, uint NumViews, [In] IntPtr[] ppShaderResourceViews);
        [PreserveSig] void GSSetSamplers(uint StartSlot, uint NumSamplers, [In] IntPtr[] ppSamplers);
        [PreserveSig] void OMSetRenderTargets(uint NumViews, [In] IntPtr[] ppRenderTargetViews, IntPtr pDepthStencilView);
        [PreserveSig] void OMSetRenderTargetsAndUnorderedAccessViews(uint NumRTVs, [In] IntPtr[] ppRenderTargetViews, IntPtr pDepthStencilView, uint UAVStartSlot, uint NumUAVs, [In] IntPtr[] ppUnorderedAccessViews, [In] uint[] pUAVInitialCounts);
        [PreserveSig] void OMSetBlendState(IntPtr pBlendState, float[] BlendFactor, uint SampleMask);
        [PreserveSig] void OMSetDepthStencilState(IntPtr pDepthStencilState, uint StencilRef);
        [PreserveSig] void SOSetTargets(uint NumTargets, [In] IntPtr[] ppSOTargets, [In] uint[] pOffsets);
        [PreserveSig] void DrawAuto();
        [PreserveSig] void DrawIndexedInstancedIndirect(IntPtr pBufferForArgs, uint AlignedByteOffsetForArgs);
        [PreserveSig] void DrawInstancedIndirect(IntPtr pBufferForArgs, uint AlignedByteOffsetForArgs);
        [PreserveSig] void Dispatch(uint ThreadGroupCountX, uint ThreadGroupCountY, uint ThreadGroupCountZ);
        [PreserveSig] void DispatchIndirect(IntPtr pBufferForArgs, uint AlignedByteOffsetForArgs);
        [PreserveSig] void RSSetState(IntPtr pRasterizerState);
        [PreserveSig] void RSSetViewports(uint NumViewports, IntPtr pViewports);
        [PreserveSig] void RSSetScissorRects(uint NumRects, IntPtr pRects);
        [PreserveSig] void CopySubresourceRegion(IntPtr pDstResource, uint DstSubresource, uint DstX, uint DstY, uint DstZ, IntPtr pSrcResource, uint SrcSubresource, IntPtr pSrcBox);
        [PreserveSig] void CopyResource(IntPtr pDstResource, IntPtr pSrcResource);
        [PreserveSig] void UpdateSubresource(IntPtr pDstResource, uint DstSubresource, IntPtr pDstBox, IntPtr pSrcData, uint SrcRowPitch, uint SrcDepthPitch);
        [PreserveSig] void CopyStructureCount(IntPtr pDstBuffer, uint DstAlignedByteOffset, IntPtr pSrcView);
        [PreserveSig] void ClearRenderTargetView(IntPtr pRenderTargetView, [In] float[] ColorRGBA);
    }

    public class DisplayRendererService
    {
        [DllImport("d3d11.dll", SetLastError = true)]
        private static extern int D3D11CreateDevice(
            IntPtr pAdapter,
            int driverType,
            IntPtr software,
            uint flags,
            [In] int[] pFeatureLevels,
            uint featureLevels,
            uint sdkVersion,
            out IntPtr ppDevice,
            out int pFeatureLevel,
            out IntPtr ppImmediateContext);

        [DllImport("dxgi.dll", SetLastError = true)]
        private static extern int CreateDXGIFactory1([In] ref Guid riid, out IntPtr ppFactory);

        private IntPtr _d3dDevice = IntPtr.Zero;
        private IntPtr _d3dContext = IntPtr.Zero;
        private IntPtr _swapChain = IntPtr.Zero;
        private IntPtr _renderTargetView = IntPtr.Zero;

        public IntPtr D3DDeviceHandle => _d3dDevice;
        public IntPtr D3DContextHandle => _d3dContext;
        public IntPtr SwapChainHandle => _swapChain;
        public bool IsInitialized => _swapChain != IntPtr.Zero && _d3dDevice != IntPtr.Zero;

        private static void Log(string msg)
        {
            System.Diagnostics.Debug.WriteLine("[DisplayRenderer] " + msg);
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "box86_display.log"), $"{DateTime.Now:HH:mm:ss.fff} {msg}{Environment.NewLine}"); } catch { }
        }

        public bool InitializeRenderer(IntPtr panelNativeHandle, int width, int height)
        {
            try
            {
                int[] featureLevels = new[]
                {
                    0xb000, // D3D_FEATURE_LEVEL_11_0
                    0xa100, // D3D_FEATURE_LEVEL_10_1
                    0xa000  // D3D_FEATURE_LEVEL_10_0
                };

                uint createDeviceFlags = 0x20; // D3D11_CREATE_DEVICE_BGRA_SUPPORT

                int hr = D3D11CreateDevice(
                    IntPtr.Zero,
                    1, // D3D_DRIVER_TYPE_HARDWARE
                    IntPtr.Zero,
                    createDeviceFlags,
                    featureLevels,
                    (uint)featureLevels.Length,
                    7, // D3D11_SDK_VERSION
                    out _d3dDevice,
                    out int level,
                    out _d3dContext);

                if (hr < 0 || _d3dDevice == IntPtr.Zero)
                    return false;

                // Create DXGI Factory 2
                Guid iidFactory2 = new("50c83a1c-e072-4c48-87b0-3630fa36a6d0");
                hr = CreateDXGIFactory1(ref iidFactory2, out IntPtr factoryPtr);
                if (hr < 0 || factoryPtr == IntPtr.Zero)
                    return false;

                var factory = (IDXGIFactory2)Marshal.GetObjectForIUnknown(factoryPtr);

                uint w = (uint)Math.Max(640, width);
                uint h = (uint)Math.Max(480, height);

                var desc = new DXGI_SWAP_CHAIN_DESC1
                {
                    Width = w,
                    Height = h,
                    Format = 87, // DXGI_FORMAT_B8G8R8A8_UNORM
                    Stereo = 0,
                    SampleDesc = new DXGI_SAMPLE_DESC { Count = 1, Quality = 0 },
                    BufferUsage = 0x20, // DXGI_USAGE_RENDER_TARGET_OUTPUT
                    BufferCount = 2,
                    Scaling = 0, // DXGI_SCALING_STRETCH
                    SwapEffect = 4, // DXGI_SWAP_EFFECT_FLIP_DISCARD
                    AlphaMode = 0, // DXGI_ALPHA_MODE_UNSPECIFIED
                    Flags = 0
                };

                hr = factory.CreateSwapChainForComposition(_d3dDevice, ref desc, IntPtr.Zero, out _swapChain);
                Marshal.Release(factoryPtr);

                if (hr < 0 || _swapChain == IntPtr.Zero)
                    return false;


                // Bind swap chain to SwapChainPanel
                if (panelNativeHandle != IntPtr.Zero)
                {
                    Guid iidPanel = typeof(ISwapChainPanelNative).GUID;
                    int qhr = Marshal.QueryInterface(panelNativeHandle, in iidPanel, out IntPtr panelNative);
                    Log($"QueryInterface(ISwapChainPanelNative) hr=0x{qhr:X8}");
                    if (qhr >= 0 && panelNative != IntPtr.Zero)
                    {
                        try
                        {
                            var nativePanel = (ISwapChainPanelNative)Marshal.GetObjectForIUnknown(panelNative);
                            int shr = nativePanel.SetSwapChain(_swapChain);
                            Log($"SetSwapChain hr=0x{shr:X8}");
                        }
                        finally
                        {
                            Marshal.Release(panelNative);
                        }
                    }
                }

                // Drawing (clear/blit/present/resize) is owned by 86box_core on its
                // own thread; touching the shared immediate context here would race
                // with it, so this service only creates and binds the swap chain.

                Log($"InitializeRenderer OK swapchain=0x{_swapChain.ToInt64():X}");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Init failed: {ex}");
                return false;
            }
        }

        private void CreateRenderTarget()
        {
            // Back-buffer views are created and owned by 86box_core.
        }

        public void RenderClear(float r = 0.04f, float g = 0.04f, float b = 0.05f, float a = 1.0f)
        {
        }

        public bool ResizeBuffers(int width, int height)
        {
            // Swap chain buffers are resized by 86box_core (emu_resize_viewport).
            return _swapChain != IntPtr.Zero;
        }

        public void BlitFrame(byte[] pixelBuffer, int x, int y, int width, int height, int pitch)
        {
            RenderClear();
        }

        public void Cleanup()
        {
            if (_renderTargetView != IntPtr.Zero) { Marshal.Release(_renderTargetView); _renderTargetView = IntPtr.Zero; }
            if (_swapChain != IntPtr.Zero) { Marshal.Release(_swapChain); _swapChain = IntPtr.Zero; }
            if (_d3dContext != IntPtr.Zero) { Marshal.Release(_d3dContext); _d3dContext = IntPtr.Zero; }
            if (_d3dDevice != IntPtr.Zero) { Marshal.Release(_d3dDevice); _d3dDevice = IntPtr.Zero; }
        }
    }
}
