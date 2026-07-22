# Third-party notices

Expeditions Macro depends on the following packages at build or runtime:

| Component | Version | License | Purpose |
| --- | --- | --- | --- |
| .NET runtime and WPF | 10 | MIT | Windows desktop runtime and UI |
| Fredoka | 2.001 static faces | SIL OFL 1.1 | Bundled application typeface |
| Lucide Icons | Lucide snapshot `658573b` | ISC / MIT | Native WPF action and navigation icons |
| OpenCV / OpenCvSharp4 | 4.13.0.20260627 | Apache-2.0 | Local image preparation and comparison |
| Vortice.Windows | 3.8.3 | MIT | Direct3D 11 and DXGI interop for window-targeted FP16 capture |
| SharpGen.Runtime | 2.4.2-beta | MIT | Native COM interop runtime used by Vortice.Windows |
| Microsoft Visual C++ Redistributable | 2015-2022 x64 | Microsoft Software License Terms | App-local native runtime required by OpenCV |
| Microsoft.NET.Test.Sdk | 17.14.1 | MIT | Test execution |
| coverlet.collector | 6.0.4 | MIT | Test coverage collection |
| xUnit.net | 2.9.3 | Apache-2.0 | Automated tests |

The distributed application includes only runtime dependencies. Test-only dependencies are listed for source-build transparency.

License texts for the bundled font and icon geometry ship under `Licenses/`. Other license texts and source information are available from each component's official repository or NuGet package metadata. This notice does not alter those licenses.
