function Ensure-UiGraphicsAssemblies {
    if (-not ("System.Drawing.Bitmap" -as [type])) {
        Add-Type -AssemblyName System.Drawing
    }
}

function Get-UiPrimaryScreenBounds {
    Ensure-UiWindowsFormsAssembly
    $screenBounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    return [pscustomobject]@{
        Left = [int]$screenBounds.Left
        Top = [int]$screenBounds.Top
        Width = [int]$screenBounds.Width
        Height = [int]$screenBounds.Height
    }
}

function Save-UiWindowScreenshot {
    param(
        [Parameter(Mandatory)]
        [System.Windows.Automation.AutomationElement]$Window,
        [Parameter(Mandatory)]
        [string]$Path
    )

    Ensure-UiGraphicsAssemblies

    $directory = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    $bounds = Get-UiBounds -Element $Window
    $bitmap = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($bounds.Left, $bounds.Top, 0, 0, $bitmap.Size)
        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }

    return $Path
}

function Save-UiDesktopScreenshot {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    Ensure-UiGraphicsAssemblies
    Ensure-UiWindowsFormsAssembly

    $directory = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    $screenBounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bitmap = New-Object System.Drawing.Bitmap $screenBounds.Width, $screenBounds.Height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($screenBounds.Left, $screenBounds.Top, 0, 0, $bitmap.Size)
        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }

    return $Path
}

function New-UiContactSheet {
    param(
        [Parameter(Mandatory)]
        [string[]]$ImagePaths,
        [Parameter(Mandatory)]
        [string]$DestinationPath,
        [int]$Columns = 3
    )

    Ensure-UiGraphicsAssemblies

    $images = foreach ($path in $ImagePaths) {
        [pscustomobject]@{
            Path = $path
            Name = [System.IO.Path]::GetFileNameWithoutExtension($path)
            Bitmap = [System.Drawing.Image]::FromFile($path)
        }
    }

    try {
        $tileWidth = [int](($images | ForEach-Object { $_.Bitmap.Width } | Measure-Object -Maximum).Maximum)
        $tileHeight = [int](($images | ForEach-Object { $_.Bitmap.Height } | Measure-Object -Maximum).Maximum)
        $rows = [int][Math]::Ceiling($images.Count / [double]$Columns)
        $padding = 16
        $labelHeight = 28
        $sheetWidth = [int](($Columns * ($tileWidth + $padding)) + $padding)
        $sheetHeight = [int](($rows * ($tileHeight + $labelHeight + $padding)) + $padding)

        $sheet = New-Object System.Drawing.Bitmap $sheetWidth, $sheetHeight
        $graphics = [System.Drawing.Graphics]::FromImage($sheet)
        $font = New-Object System.Drawing.Font "Segoe UI", 10, ([System.Drawing.FontStyle]::Regular)
        $textBrush = [System.Drawing.Brushes]::Black
        try {
            $graphics.Clear([System.Drawing.Color]::White)
            for ($index = 0; $index -lt $images.Count; $index++) {
                $row = [int]($index / $Columns)
                $column = $index % $Columns
                $x = $padding + ($column * ($tileWidth + $padding))
                $y = $padding + ($row * ($tileHeight + $labelHeight + $padding))
                $graphics.DrawImage($images[$index].Bitmap, $x, $y, $images[$index].Bitmap.Width, $images[$index].Bitmap.Height)
                $graphics.DrawString($images[$index].Name, $font, $textBrush, $x, $y + $tileHeight + 4)
            }

            $directory = Split-Path -Parent $DestinationPath
            if (-not (Test-Path -LiteralPath $directory)) {
                New-Item -ItemType Directory -Force -Path $directory | Out-Null
            }

            $sheet.Save($DestinationPath, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $font.Dispose()
            $graphics.Dispose()
            $sheet.Dispose()
        }
    }
    finally {
        foreach ($image in $images) {
            $image.Bitmap.Dispose()
        }
    }

    return $DestinationPath
}

function Compare-UiImages {
    param(
        [Parameter(Mandatory)]
        [string]$ReferencePath,
        [Parameter(Mandatory)]
        [string]$ActualPath,
        [string]$DiffPath,
        [int]$Tolerance = 18,
        [int]$SampleStep = 2
    )

    Ensure-UiGraphicsAssemblies

    if (-not (Test-Path -LiteralPath $ReferencePath)) {
        throw "Reference image was not found: $ReferencePath"
    }

    if (-not (Test-Path -LiteralPath $ActualPath)) {
        throw "Actual image was not found: $ActualPath"
    }

    $referenceImage = [System.Drawing.Image]::FromFile($ReferencePath)
    $actualImage = [System.Drawing.Image]::FromFile($ActualPath)
    $referenceBitmap = $null
    $actualBitmap = $null
    $resizedReference = $null
    $diffBitmap = $null

    try {
        $referenceBitmap = New-Object System.Drawing.Bitmap $referenceImage
        $actualBitmap = New-Object System.Drawing.Bitmap $actualImage

        if ($referenceBitmap.Width -ne $actualBitmap.Width -or $referenceBitmap.Height -ne $actualBitmap.Height) {
            $resizedReference = New-Object System.Drawing.Bitmap $actualBitmap.Width, $actualBitmap.Height
            $graphics = [System.Drawing.Graphics]::FromImage($resizedReference)
            try {
                $graphics.DrawImage($referenceBitmap, 0, 0, $actualBitmap.Width, $actualBitmap.Height)
            }
            finally {
                $graphics.Dispose()
            }

            $referenceBitmap.Dispose()
            $referenceBitmap = $resizedReference
            $resizedReference = $null
        }

        $width = $actualBitmap.Width
        $height = $actualBitmap.Height
        $step = [Math]::Max(1, $SampleStep)
        $sampledPixels = 0
        $changedPixels = 0
        $minX = $width
        $minY = $height
        $maxX = -1
        $maxY = -1

        if (-not [string]::IsNullOrWhiteSpace($DiffPath)) {
            $diffBitmap = New-Object System.Drawing.Bitmap $width, $height
            $graphics = [System.Drawing.Graphics]::FromImage($diffBitmap)
            try {
                $graphics.DrawImage($actualBitmap, 0, 0, $width, $height)
            }
            finally {
                $graphics.Dispose()
            }
        }

        for ($y = 0; $y -lt $height; $y += $step) {
            for ($x = 0; $x -lt $width; $x += $step) {
                $sampledPixels++
                $expected = $referenceBitmap.GetPixel($x, $y)
                $actual = $actualBitmap.GetPixel($x, $y)
                $delta = [Math]::Abs($expected.R - $actual.R) + [Math]::Abs($expected.G - $actual.G) + [Math]::Abs($expected.B - $actual.B)
                if ($delta -gt $Tolerance) {
                    $changedPixels++
                    if ($x -lt $minX) { $minX = $x }
                    if ($y -lt $minY) { $minY = $y }
                    if ($x -gt $maxX) { $maxX = $x }
                    if ($y -gt $maxY) { $maxY = $y }

                    if ($null -ne $diffBitmap) {
                        $markColor = [System.Drawing.Color]::FromArgb(220, 255, 59, 48)
                        for ($offsetY = 0; $offsetY -lt $step -and ($y + $offsetY) -lt $height; $offsetY++) {
                            for ($offsetX = 0; $offsetX -lt $step -and ($x + $offsetX) -lt $width; $offsetX++) {
                                $diffBitmap.SetPixel($x + $offsetX, $y + $offsetY, $markColor)
                            }
                        }
                    }
                }
            }
        }

        $differenceRatio = if ($sampledPixels -gt 0) { [math]::Round(($changedPixels / [double]$sampledPixels) * 100.0, 2) } else { 0 }
        if ($null -ne $diffBitmap -and -not [string]::IsNullOrWhiteSpace($DiffPath)) {
            $directory = Split-Path -Parent $DiffPath
            if (-not (Test-Path -LiteralPath $directory)) {
                New-Item -ItemType Directory -Force -Path $directory | Out-Null
            }

            $diffBitmap.Save($DiffPath, [System.Drawing.Imaging.ImageFormat]::Png)
        }

        return [pscustomobject]@{
            ReferencePath = [System.IO.Path]::GetFullPath($ReferencePath)
            ActualPath = [System.IO.Path]::GetFullPath($ActualPath)
            DiffPath = if (-not [string]::IsNullOrWhiteSpace($DiffPath) -and (Test-Path -LiteralPath $DiffPath)) { [System.IO.Path]::GetFullPath($DiffPath) } else { $null }
            Width = $width
            Height = $height
            SampleStep = $step
            Tolerance = $Tolerance
            SampledPixels = $sampledPixels
            ChangedPixels = $changedPixels
            DifferenceRatio = $differenceRatio
            DifferenceBounds = if ($changedPixels -gt 0) {
                [pscustomobject]@{
                    Left = $minX
                    Top = $minY
                    Right = $maxX
                    Bottom = $maxY
                    Width = ($maxX - $minX) + 1
                    Height = ($maxY - $minY) + 1
                }
            }
            else {
                $null
            }
        }
    }
    finally {
        if ($null -ne $diffBitmap) { $diffBitmap.Dispose() }
        if ($null -ne $referenceBitmap) { $referenceBitmap.Dispose() }
        if ($null -ne $actualBitmap) { $actualBitmap.Dispose() }
        if ($null -ne $resizedReference) { $resizedReference.Dispose() }
        $referenceImage.Dispose()
        $actualImage.Dispose()
    }
}
