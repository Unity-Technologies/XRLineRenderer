# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [0.1.3-preview] - 2021-05-27
- Add api "RefreshMesh" to MeshChainRenderer to manually trigger the mesh to be updated. 
  This is normally already done if needed in LateUpdate, but this method can be used to force the mesh to update, such as in Application.BeforeRender to stay aligned with an XR controller updating pose during that callback

## [0.1.2-preview] - 2020-07-31
- Replace "Labs" with "XRTools" in package name and namespaces

## [0.1.1-preview] - 2020-01-03
Clean up for package release

## [0.1.0-preview.1] - 2019-12-09
Fix an issue where m_Width was not taken into account for startWidth and endWidth properties

## [0.1.0-preview] - 2019-11-25

### This is the first release of *Unity Package \<com.unity.xr-line-renderer\>*.
