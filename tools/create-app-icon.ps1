# Copyright 2026 Mr_Aec. Licensed under Apache-2.0.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$taskRoot = Split-Path $PSScriptRoot -Parent
function New-RoundedPath([float]$X, [float]$Y, [float]$Width, [float]$Height, [float]$Radius) {
 $taskPath = [Drawing.Drawing2D.GraphicsPath]::new()
 $taskDiameter = $Radius * 2
 $taskPath.AddArc($X, $Y, $taskDiameter, $taskDiameter, 180, 90)
 $taskPath.AddArc($X + $Width - $taskDiameter, $Y, $taskDiameter, $taskDiameter, 270, 90)
 $taskPath.AddArc($X + $Width - $taskDiameter, $Y + $Height - $taskDiameter, $taskDiameter, $taskDiameter, 0, 90)
 $taskPath.AddArc($X, $Y + $Height - $taskDiameter, $taskDiameter, $taskDiameter, 90, 90)
 $taskPath.CloseFigure()
 return $taskPath
}
$taskBitmap = [Drawing.Bitmap]::new(1024, 1024)
$taskGraphics = [Drawing.Graphics]::FromImage($taskBitmap)
$taskGraphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
$taskGraphics.Clear([Drawing.Color]::Transparent)
$taskGraphics.ScaleTransform(4, 4)
$taskBackground = New-RoundedPath 12 12 232 232 52
$taskGradient = [Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.RectangleF]::new(12,12,232,232), [Drawing.ColorTranslator]::FromHtml('#173754'), [Drawing.ColorTranslator]::FromHtml('#081320'), 65)
$taskGraphics.FillPath($taskGradient, $taskBackground)
$taskEdge = [Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#2B536D'), 2)
$taskGraphics.DrawPath($taskEdge, $taskBackground)
$taskState = $taskGraphics.Save()
$taskGraphics.TranslateTransform(111, 142)
$taskGraphics.RotateTransform(-15)
$taskGraphics.TranslateTransform(-111, -142)
$taskBack = New-RoundedPath 52 66 106 145 16
$taskBackBrush = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#143556'))
$taskBackPen = [Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#60A9FF'), 5)
$taskGraphics.FillPath($taskBackBrush, $taskBack)
$taskGraphics.DrawPath($taskBackPen, $taskBack)
$taskGraphics.Restore($taskState)
$taskFront = New-RoundedPath 91 47 112 151 17
$taskFrontBrush = [Drawing.Drawing2D.LinearGradientBrush]::new([Drawing.RectangleF]::new(91,47,112,151), [Drawing.ColorTranslator]::FromHtml('#4EA2FF'), [Drawing.ColorTranslator]::FromHtml('#1761DB'), 80)
$taskFrontPen = [Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#BFDEFF'), 3)
$taskGraphics.FillPath($taskFrontBrush, $taskFront)
$taskGraphics.DrawPath($taskFrontPen, $taskFront)
$taskLinePen = [Drawing.Pen]::new([Drawing.Color]::FromArgb(235,255,255,255), 6)
$taskLinePen.StartCap = $taskLinePen.EndCap = [Drawing.Drawing2D.LineCap]::Round
$taskGraphics.DrawLine($taskLinePen, 111, 74, 147, 74)
$taskGraphics.DrawLine($taskLinePen, 111, 90, 136, 90)
$taskBadgeBrush = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#56E6C8'))
$taskBadgePen = [Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#0C2535'), 5)
$taskGraphics.FillEllipse($taskBadgeBrush, 137, 132, 76, 76)
$taskGraphics.DrawEllipse($taskBadgePen, 137, 132, 76, 76)
$taskInk = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#0B2C3A'))
$taskTriangle = [Drawing.PointF[]]@([Drawing.PointF]::new(164,151),[Drawing.PointF]::new(164,189),[Drawing.PointF]::new(192,170))
$taskGraphics.FillPolygon($taskInk, $taskTriangle)
$taskBitmap.Save((Join-Path $taskRoot 'resources/SteamCardPilot.png'), [Drawing.Imaging.ImageFormat]::Png)
$taskFrames = @()
foreach ($taskSize in @(16,24,32,48,64,128,256)) {
 $taskFrame = [Drawing.Bitmap]::new($taskSize, $taskSize)
 $taskFrameGraphics = [Drawing.Graphics]::FromImage($taskFrame)
 $taskFrameGraphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
 $taskFrameGraphics.DrawImage($taskBitmap, 0, 0, $taskSize, $taskSize)
 $taskStream = [IO.MemoryStream]::new()
 $taskFrame.Save($taskStream, [Drawing.Imaging.ImageFormat]::Png)
 $taskFrames += @{ Size=$taskSize; Bytes=$taskStream.ToArray() }
 $taskStream.Dispose(); $taskFrameGraphics.Dispose(); $taskFrame.Dispose()
}
$taskWriter = [IO.BinaryWriter]::new([IO.File]::Create((Join-Path $taskRoot 'resources/SteamCardPilot.ico')))
try {
 $taskWriter.Write([uint16]0); $taskWriter.Write([uint16]1); $taskWriter.Write([uint16]$taskFrames.Count)
 $taskOffset = 6 + 16 * $taskFrames.Count
 foreach ($taskFrame in $taskFrames) {
  $taskSizeByte = if ($taskFrame.Size -eq 256) { 0 } else { $taskFrame.Size }
  $taskWriter.Write([byte]$taskSizeByte); $taskWriter.Write([byte]$taskSizeByte)
  $taskWriter.Write([byte]0); $taskWriter.Write([byte]0)
  $taskWriter.Write([uint16]1); $taskWriter.Write([uint16]32)
  $taskWriter.Write([uint32]$taskFrame.Bytes.Length); $taskWriter.Write([uint32]$taskOffset)
  $taskOffset += $taskFrame.Bytes.Length
 }
 foreach ($taskFrame in $taskFrames) { $taskWriter.Write([byte[]]$taskFrame.Bytes) }
} finally { $taskWriter.Dispose() }
foreach ($taskDisposable in @($taskGraphics,$taskBitmap,$taskBackground,$taskGradient,$taskEdge,$taskBack,$taskBackBrush,$taskBackPen,$taskFront,$taskFrontBrush,$taskFrontPen,$taskLinePen,$taskBadgeBrush,$taskBadgePen,$taskInk)) { $taskDisposable.Dispose() }
Write-Host 'Created the 1024px logo and a seven-size Windows icon.'
