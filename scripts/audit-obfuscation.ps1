param(
    [string]$AssemblyPath = "C:\Games\Steam\steamapps\common\Travellers Rest\Windows\TravellersRest_Data\Managed\Assembly-CSharp.dll"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $AssemblyPath)) {
    throw "Assembly not found: $AssemblyPath"
}

$assembly = [Reflection.Assembly]::LoadFrom($AssemblyPath)
$allFlags = [Reflection.BindingFlags]"Public,NonPublic,Static,Instance"
$declaredInstanceFlags = [Reflection.BindingFlags]"Public,NonPublic,Instance,DeclaredOnly"

$oneByteOpCodes = New-Object 'System.Reflection.Emit.OpCode[]' 256
$twoByteOpCodes = New-Object 'System.Reflection.Emit.OpCode[]' 256
foreach ($field in [System.Reflection.Emit.OpCodes].GetFields([Reflection.BindingFlags]"Public,Static")) {
    $op = [System.Reflection.Emit.OpCode]$field.GetValue($null)
    $value = ([int]$op.Value) -band 0xffff
    if ($value -lt 256) {
        $oneByteOpCodes[$value] = $op
    } elseif (($value -band 0xff00) -eq 0xfe00) {
        $twoByteOpCodes[$value -band 0xff] = $op
    }
}

function Get-TypeSafe {
    param([string]$TypeName)
    return $assembly.GetType($TypeName)
}

function Test-MemberByName {
    param(
        [string]$TypeName,
        [string]$MemberName,
        [ValidateSet("Property", "Field", "Method")]
        [string]$MemberKind
    )

    $type = Get-TypeSafe $TypeName
    if (-not $type) { return $false }

    switch ($MemberKind) {
        "Property" { return @($type.GetProperties($allFlags) | Where-Object { $_.Name -eq $MemberName }).Count -gt 0 }
        "Field" { return @($type.GetFields($allFlags) | Where-Object { $_.Name -eq $MemberName }).Count -gt 0 }
        "Method" { return @($type.GetMethods($allFlags) | Where-Object { $_.Name -eq $MemberName }).Count -gt 0 }
    }
}

function Test-StaticMemberOfType {
    param(
        [string]$TypeName,
        [string]$ValueTypeName
    )

    $type = Get-TypeSafe $TypeName
    if (-not $type) { return $false }

    foreach ($prop in $type.GetProperties($allFlags)) {
        if ($prop.PropertyType.Name -eq $ValueTypeName -and $prop.GetMethod -and $prop.GetMethod.IsStatic -and $prop.GetIndexParameters().Count -eq 0) {
            return $true
        }
    }

    foreach ($field in $type.GetFields($allFlags)) {
        if ($field.FieldType.Name -eq $ValueTypeName -and $field.IsStatic) {
            return $true
        }
    }

    return $false
}

function Test-InstancePropertyOfType {
    param(
        [string]$TypeName,
        [string]$ValueTypeName
    )

    $type = Get-TypeSafe $TypeName
    if (-not $type) { return $false }

    return @(
        $type.GetProperties($allFlags) |
        Where-Object {
            $_.PropertyType.Name -eq $ValueTypeName -and
            $_.GetMethod -and
            -not $_.GetMethod.IsStatic -and
            $_.GetIndexParameters().Count -eq 0
        }
    ).Count -gt 0
}

function Test-InstanceFieldOfType {
    param(
        [string]$TypeName,
        [string]$ValueTypeName
    )

    $type = Get-TypeSafe $TypeName
    if (-not $type) { return $false }

    return @(
        $type.GetFields($allFlags) |
        Where-Object {
            $_.FieldType.Name -eq $ValueTypeName -and
            -not $_.IsStatic
        }
    ).Count -gt 0
}

function Get-OperandSize {
    param(
        [System.Reflection.Emit.OperandType]$OperandType,
        [byte[]]$IL,
        [int]$Index
    )

    switch ($OperandType) {
        ([System.Reflection.Emit.OperandType]::InlineNone) { return 0 }
        ([System.Reflection.Emit.OperandType]::ShortInlineBrTarget) { return 1 }
        ([System.Reflection.Emit.OperandType]::ShortInlineI) { return 1 }
        ([System.Reflection.Emit.OperandType]::ShortInlineVar) { return 1 }
        ([System.Reflection.Emit.OperandType]::InlineVar) { return 2 }
        ([System.Reflection.Emit.OperandType]::InlineI) { return 4 }
        ([System.Reflection.Emit.OperandType]::InlineBrTarget) { return 4 }
        ([System.Reflection.Emit.OperandType]::InlineField) { return 4 }
        ([System.Reflection.Emit.OperandType]::InlineMethod) { return 4 }
        ([System.Reflection.Emit.OperandType]::InlineSig) { return 4 }
        ([System.Reflection.Emit.OperandType]::InlineString) { return 4 }
        ([System.Reflection.Emit.OperandType]::InlineTok) { return 4 }
        ([System.Reflection.Emit.OperandType]::InlineType) { return 4 }
        ([System.Reflection.Emit.OperandType]::ShortInlineR) { return 4 }
        ([System.Reflection.Emit.OperandType]::InlineI8) { return 8 }
        ([System.Reflection.Emit.OperandType]::InlineR) { return 8 }
        ([System.Reflection.Emit.OperandType]::InlineSwitch) { return 4 + ([BitConverter]::ToInt32($IL, $Index) * 4) }
        default { return 0 }
    }
}

function Test-MethodReferencesMember {
    param(
        [Reflection.MethodInfo]$Method,
        [string]$DeclaringTypeName,
        [string]$MemberName
    )

    $body = $Method.GetMethodBody()
    if ($body -eq $null) { return $false }

    $il = $body.GetILAsByteArray()
    $module = $Method.Module
    $index = 0

    while ($index -lt $il.Length) {
        $value = $il[$index]
        $index++

        if ($value -eq 0xfe) {
            $op = $twoByteOpCodes[$il[$index]]
            $index++
        } else {
            $op = $oneByteOpCodes[$value]
        }

        $operandStart = $index
        $operandSize = Get-OperandSize -OperandType $op.OperandType -IL $il -Index $operandStart

        if ($op.OperandType -in @(
            [System.Reflection.Emit.OperandType]::InlineField,
            [System.Reflection.Emit.OperandType]::InlineMethod,
            [System.Reflection.Emit.OperandType]::InlineTok
        )) {
            $member = $null
            try {
                $token = [BitConverter]::ToInt32($il, $operandStart)
                if ($op.OperandType -eq [System.Reflection.Emit.OperandType]::InlineField) {
                    $member = $module.ResolveField($token)
                } else {
                    $member = $module.ResolveMember($token)
                }
            } catch {}

            if ($member -and $member.DeclaringType -and $member.DeclaringType.Name -eq $DeclaringTypeName -and $member.Name -eq $MemberName) {
                return $true
            }
        }

        $index += $operandSize
    }

    return $false
}

function Test-WellWaterTargets {
    $type = Get-TypeSafe "Well"
    if (-not $type) { return $false }

    foreach ($method in $type.GetMethods($declaredInstanceFlags)) {
        $parameters = $method.GetParameters()
        if ($method.ReturnType -ne [bool]) { continue }
        if ($parameters.Length -ne 1 -or $parameters[0].ParameterType -ne [int]) { continue }

        if ((Test-MethodReferencesMember -Method $method -DeclaringTypeName "CommonReferences" -MemberName "bucketItem") -and
            (Test-MethodReferencesMember -Method $method -DeclaringTypeName "CommonReferences" -MemberName "bucketOfWaterItem")) {
            return $true
        }
    }

    return $false
}

function Test-CrafterReturnBucketTargets {
    $type = Get-TypeSafe "Crafter"
    if (-not $type) { return $false }

    foreach ($method in $type.GetMethods($declaredInstanceFlags)) {
        $parameters = $method.GetParameters()
        if ($method.ReturnType -ne [void]) { continue }
        if ($parameters.Length -ne 2) { continue }
        if ($parameters[0].ParameterType -ne [int] -or $parameters[1].ParameterType.Name -ne "ItemAmount") { continue }

        if (Test-MethodReferencesMember -Method $method -DeclaringTypeName "PlayerInventory" -MemberName "AddItems") {
            return $true
        }
    }

    return $false
}

$targets = @(
    [pscustomobject]@{ Plugin = "TRTracker"; Check = "TavernManager singleton"; Found = Test-MemberByName -TypeName "TavernManager" -MemberName "GOKBJFAMHMJ" -MemberKind "Property" },
    [pscustomobject]@{ Plugin = "TRTracker"; Check = "TavernReputation singleton"; Found = Test-MemberByName -TypeName "TavernReputation" -MemberName "GOKBJFAMHMJ" -MemberKind "Property" },
    [pscustomobject]@{ Plugin = "TRTracker"; Check = "TavernServiceManager singleton"; Found = Test-MemberByName -TypeName "TavernServiceManager" -MemberName "GOKBJFAMHMJ" -MemberKind "Property" },
    [pscustomobject]@{ Plugin = "TRTracker"; Check = "TavernZonesManager singleton"; Found = Test-MemberByName -TypeName "TavernZonesManager" -MemberName "GOKBJFAMHMJ" -MemberKind "Property" },
    [pscustomobject]@{ Plugin = "TRTracker"; Check = "WorldTime GameDate source"; Found = Test-StaticMemberOfType -TypeName "WorldTime" -ValueTypeName "GameDate" },
    [pscustomobject]@{ Plugin = "TRTracker"; Check = "WorldTime absolute time source"; Found = Test-StaticMemberOfType -TypeName "WorldTime" -ValueTypeName "UInt64" },
    [pscustomobject]@{ Plugin = "TRTracker"; Check = "WorldTime multiplier field"; Found = Test-MemberByName -TypeName "WorldTime" -MemberName "multiplier" -MemberKind "Field" },
    [pscustomobject]@{ Plugin = "TRTracker"; Check = "WorldTime.SetTimeMultiplier"; Found = Test-MemberByName -TypeName "WorldTime" -MemberName "SetTimeMultiplier" -MemberKind "Method" },
    [pscustomobject]@{ Plugin = "TRTracker"; Check = "TavernReputation level property"; Found = Test-InstancePropertyOfType -TypeName "TavernReputation" -ValueTypeName "Int32" },
    [pscustomobject]@{ Plugin = "TRTracker"; Check = "TavernReputation XP field"; Found = Test-InstanceFieldOfType -TypeName "TavernReputation" -ValueTypeName "Int32" },
    [pscustomobject]@{ Plugin = "TRTracker"; Check = "TavernManager heat property"; Found = Test-InstancePropertyOfType -TypeName "TavernManager" -ValueTypeName "HeatLevel" },
    [pscustomobject]@{ Plugin = "TRTracker"; Check = "TavernManager dirt property"; Found = Test-InstancePropertyOfType -TypeName "TavernManager" -ValueTypeName "DirtLevel" },
    [pscustomobject]@{ Plugin = "TRTracker"; Check = "TavernManager open hook"; Found = Test-MemberByName -TypeName "TavernManager" -MemberName "get_open" -MemberKind "Method" },
    [pscustomobject]@{ Plugin = "TRBar"; Check = "TavernManager open field"; Found = Test-MemberByName -TypeName "TavernManager" -MemberName "_open" -MemberKind "Field" },
    [pscustomobject]@{ Plugin = "TRBar"; Check = "DrinkDispenser slots"; Found = Test-MemberByName -TypeName "DrinkDispenser" -MemberName "slots" -MemberKind "Field" },
    [pscustomobject]@{ Plugin = "TRBar"; Check = "BarMenuInventory.GetInstance"; Found = Test-MemberByName -TypeName "BarMenuInventory" -MemberName "GetInstance" -MemberKind "Method" },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "CropSetter crop property"; Found = Test-InstancePropertyOfType -TypeName "CropSetter" -ValueTypeName "Crop" },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "Crafter fuel property"; Found = Test-InstancePropertyOfType -TypeName "Crafter" -ValueTypeName "Int32" },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "Crafter.SetFuel"; Found = Test-MemberByName -TypeName "Crafter" -MemberName "SetFuel" -MemberKind "Method" },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "Dynamic crafter bucket-return target"; Found = Test-CrafterReturnBucketTargets },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "Dynamic well water target"; Found = Test-WellWaterTargets },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "CommonReferences singleton"; Found = Test-MemberByName -TypeName "CommonReferences" -MemberName "GOKBJFAMHMJ" -MemberKind "Property" },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "CommonReferences.bucketItem"; Found = Test-MemberByName -TypeName "CommonReferences" -MemberName "bucketItem" -MemberKind "Field" },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "CommonReferences.bucketOfWaterItem"; Found = Test-MemberByName -TypeName "CommonReferences" -MemberName "bucketOfWaterItem" -MemberKind "Field" },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "Item.nameId"; Found = Test-MemberByName -TypeName "Item" -MemberName "nameId" -MemberKind "Field" },
    [pscustomobject]@{ Plugin = "TRBarrels"; Check = "AgingBarrel.inputSlot"; Found = Test-MemberByName -TypeName "AgingBarrel" -MemberName "inputSlot" -MemberKind "Field" },
    [pscustomobject]@{ Plugin = "TRBarrels"; Check = "AgingBarrel.timer"; Found = Test-MemberByName -TypeName "AgingBarrel" -MemberName "timer" -MemberKind "Field" }
)

Write-Host "Live assembly obfuscation audit" -ForegroundColor Cyan
Write-Host "Assembly: $AssemblyPath" -ForegroundColor Cyan
Write-Host ""

$missing = $targets | Where-Object { -not $_.Found }

foreach ($target in $targets) {
    $status = if ($target.Found) { "FOUND" } else { "MISSING" }
    $color = if ($target.Found) { "Green" } else { "Red" }
    Write-Host ("[{0}] {1} :: {2}" -f $status, $target.Plugin, $target.Check) -ForegroundColor $color
}

Write-Host ""
if ($missing.Count -eq 0) {
    Write-Host "All tracked live-assembly checks passed." -ForegroundColor Green
} else {
    Write-Host ("Missing checks: {0}" -f $missing.Count) -ForegroundColor Red
    $missing | ForEach-Object {
        Write-Host ("- {0} :: {1}" -f $_.Plugin, $_.Check) -ForegroundColor Red
    }
}
