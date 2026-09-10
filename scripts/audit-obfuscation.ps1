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

# True when the method contains a stfld to DeclaringTypeName::FieldName.
function Test-MethodStoresField {
    param(
        [Reflection.MethodInfo]$Method,
        [string]$DeclaringTypeName,
        [string]$FieldName
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

        if ($op -eq [System.Reflection.Emit.OpCodes]::Stfld) {
            $field = $null
            try { $field = $module.ResolveField([BitConverter]::ToInt32($il, $operandStart)) } catch {}
            if ($field -and $field.DeclaringType -and $field.DeclaringType.Name -eq $DeclaringTypeName -and $field.Name -eq $FieldName) {
                return $true
            }
        }

        $index += $operandSize
    }

    return $false
}

# Mirrors PatchTargetFinder.FindFuelWriters: void X(int, bool) methods that store the fuel field.
# TRStats patches every one of them, because the game spends fuel through SetFuel and its clones.
function Get-FuelWriterCount {
    param([string]$TypeName)

    $type = Get-TypeSafe $TypeName
    if (-not $type) { return 0 }

    $count = 0
    foreach ($method in $type.GetMethods($declaredInstanceFlags)) {
        if ($method.ReturnType -ne [void]) { continue }
        $parameters = $method.GetParameters()
        if ($parameters.Length -ne 2) { continue }
        if ($parameters[0].ParameterType -ne [int] -or $parameters[1].ParameterType -ne [bool]) { continue }
        if (Test-MethodStoresField -Method $method -DeclaringTypeName $TypeName -FieldName "fuel") { $count++ }
    }
    return $count
}

# Mirrors StatsManager.FindItemInstanceFactory: the no-arg ItemInstance factory on Item that the
# given item type overrides. Without it, spawned fuel would be a plain ItemInstance.
function Test-ItemFactoryOverride {
    param([string]$ItemTypeName)

    $item = Get-TypeSafe "Item"
    $itemType = Get-TypeSafe $ItemTypeName
    $instanceType = Get-TypeSafe "ItemInstance"
    if (-not $item -or -not $itemType -or -not $instanceType) { return $false }

    foreach ($method in $item.GetMethods([Reflection.BindingFlags]"Public,Instance,DeclaredOnly")) {
        if ($method.GetParameters().Length -ne 0 -or -not $method.IsVirtual) { continue }
        if (-not $instanceType.IsAssignableFrom($method.ReturnType)) { continue }
        $override = $itemType.GetMethod($method.Name, [Reflection.BindingFlags]"Public,Instance", $null, [Type[]]@(), $null)
        if ($override -and $override.DeclaringType -ne $item) { return $true }
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
    [pscustomobject]@{ Plugin = "TRTracker"; Check = "TavernManager singleton"; Found = Test-StaticMemberOfType -TypeName "TavernManager" -ValueTypeName "TavernManager" },
    [pscustomobject]@{ Plugin = "TRTracker"; Check = "TavernReputation singleton"; Found = Test-StaticMemberOfType -TypeName "TavernReputation" -ValueTypeName "TavernReputation" },
    [pscustomobject]@{ Plugin = "TRTracker"; Check = "TavernServiceManager singleton"; Found = Test-StaticMemberOfType -TypeName "TavernServiceManager" -ValueTypeName "TavernServiceManager" },
    [pscustomobject]@{ Plugin = "TRTracker"; Check = "TavernZonesManager singleton"; Found = Test-StaticMemberOfType -TypeName "TavernZonesManager" -ValueTypeName "TavernZonesManager" },
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
    [pscustomobject]@{ Plugin = "TRBar"; Check = "Food.halloweenFood"; Found = Test-MemberByName -TypeName "Food" -MemberName "halloweenFood" -MemberKind "Field" },
    [pscustomobject]@{ Plugin = "TRBar"; Check = "HalloweenEvent type"; Found = ($null -ne $assembly.GetType("HalloweenEvent")) },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "CropSetter crop property"; Found = Test-InstancePropertyOfType -TypeName "CropSetter" -ValueTypeName "Crop" },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "Crafter.fuel field"; Found = Test-MemberByName -TypeName "Crafter" -MemberName "fuel" -MemberKind "Field" },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "Crafter.SetFuel"; Found = Test-MemberByName -TypeName "Crafter" -MemberName "SetFuel" -MemberKind "Method" },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "Dynamic crafter fuel writers"; Found = ((Get-FuelWriterCount -TypeName "Crafter") -gt 0) },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "Crafter.arcaneCrafter"; Found = Test-MemberByName -TypeName "Crafter" -MemberName "arcaneCrafter" -MemberKind "Field" },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "MagicBookStand.fuel field"; Found = Test-MemberByName -TypeName "MagicBookStand" -MemberName "fuel" -MemberKind "Field" },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "MagicBookStand.SetFuel"; Found = Test-MemberByName -TypeName "MagicBookStand" -MemberName "SetFuel" -MemberKind "Method" },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "Dynamic book stand fuel writers"; Found = ((Get-FuelWriterCount -TypeName "MagicBookStand") -gt 0) },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "Fuel.isMagical"; Found = Test-MemberByName -TypeName "Fuel" -MemberName "isMagical" -MemberKind "Field" },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "Fuel.fuelAmount"; Found = Test-MemberByName -TypeName "Fuel" -MemberName "fuelAmount" -MemberKind "Field" },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "ItemDatabase.items"; Found = Test-MemberByName -TypeName "ItemDatabase" -MemberName "items" -MemberKind "Field" },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "DroppedItem.SpawnDroppedItem"; Found = Test-MemberByName -TypeName "DroppedItem" -MemberName "SpawnDroppedItem" -MemberKind "Method" },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "Item factory overridden by Fuel"; Found = Test-ItemFactoryOverride -ItemTypeName "Fuel" },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "Item factory overridden by Food"; Found = Test-ItemFactoryOverride -ItemTypeName "Food" },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "Dynamic crafter bucket-return target"; Found = Test-CrafterReturnBucketTargets },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "Dynamic well water target"; Found = Test-WellWaterTargets },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "CommonReferences singleton"; Found = Test-StaticMemberOfType -TypeName "CommonReferences" -ValueTypeName "CommonReferences" },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "CommonReferences.bucketItem"; Found = Test-MemberByName -TypeName "CommonReferences" -MemberName "bucketItem" -MemberKind "Field" },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "CommonReferences.bucketOfWaterItem"; Found = Test-MemberByName -TypeName "CommonReferences" -MemberName "bucketOfWaterItem" -MemberKind "Field" },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "AnimalFeeder.CanFillWithWater"; Found = Test-MemberByName -TypeName "AnimalFeeder" -MemberName "CanFillWithWater" -MemberKind "Method" },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "AnimalFeederWater.FillFeeder"; Found = Test-MemberByName -TypeName "AnimalFeederWater" -MemberName "FillFeeder" -MemberKind "Method" },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "AnimalFeederWaterHenHouse.currentAmount"; Found = Test-MemberByName -TypeName "AnimalFeederWaterHenHouse" -MemberName "currentAmount" -MemberKind "Field" },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "AnimalFeeder.maxAmount"; Found = Test-MemberByName -TypeName "AnimalFeeder" -MemberName "maxAmount" -MemberKind "Field" },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "Container.allowedItemsList"; Found = Test-MemberByName -TypeName "Container" -MemberName "allowedItemsList" -MemberKind "Field" },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "Container.GetNumberOfItems"; Found = Test-MemberByName -TypeName "Container" -MemberName "GetNumberOfItems" -MemberKind "Method" },
    [pscustomobject]@{ Plugin = "TRStats"; Check = "Item.nameId"; Found = Test-MemberByName -TypeName "Item" -MemberName "nameId" -MemberKind "Field" },
    [pscustomobject]@{ Plugin = "TRBarrels"; Check = "AgingBarrel.inputSlot"; Found = Test-MemberByName -TypeName "AgingBarrel" -MemberName "inputSlot" -MemberKind "Field" },
    [pscustomobject]@{ Plugin = "TRBarrels"; Check = "AgingBarrel.timer"; Found = Test-MemberByName -TypeName "AgingBarrel" -MemberName "timer" -MemberKind "Field" },
    [pscustomobject]@{ Plugin = "TRBarrels"; Check = "FoodInstance aging-level member (obfuscated canary: JBLCDOEDODA, prop-or-field)"; Found = ((Test-MemberByName -TypeName "FoodInstance" -MemberName "JBLCDOEDODA" -MemberKind "Property") -or (Test-MemberByName -TypeName "FoodInstance" -MemberName "JBLCDOEDODA" -MemberKind "Field")) },
    [pscustomobject]@{ Plugin = "TRAutoloader"; Check = "FoodInstance cached-price field (obfuscated canary: KEPEKHAMHBI)"; Found = Test-MemberByName -TypeName "FoodInstance" -MemberName "KEPEKHAMHBI" -MemberKind "Field" },
    [pscustomobject]@{ Plugin = "TRAutoloader"; Check = "DrinkDispenser.isBeerTap"; Found = Test-MemberByName -TypeName "DrinkDispenser" -MemberName "isBeerTap" -MemberKind "Field" },
    [pscustomobject]@{ Plugin = "TRAutoloader"; Check = "DrinkDispenser slots"; Found = Test-MemberByName -TypeName "DrinkDispenser" -MemberName "slots" -MemberKind "Field" },
    [pscustomobject]@{ Plugin = "TRAutoloader"; Check = "BanquetBarrel slots"; Found = Test-MemberByName -TypeName "BanquetBarrel" -MemberName "slots" -MemberKind "Field" },
    [pscustomobject]@{ Plugin = "TRAutoloader"; Check = "BarMenuInventory.GetInstance"; Found = Test-MemberByName -TypeName "BarMenuInventory" -MemberName "GetInstance" -MemberKind "Method" },
    [pscustomobject]@{ Plugin = "TRAutoloader"; Check = "TavernZonesManager singleton"; Found = Test-StaticMemberOfType -TypeName "TavernZonesManager" -ValueTypeName "TavernZonesManager" },
    [pscustomobject]@{ Plugin = "TRAutoloader"; Check = "TavernZone.zoneType"; Found = Test-MemberByName -TypeName "TavernZone" -MemberName "zoneType" -MemberKind "Field" },
    [pscustomobject]@{ Plugin = "TRAutoloader"; Check = "Placeable.guidString"; Found = Test-MemberByName -TypeName "Placeable" -MemberName "guidString" -MemberKind "Field" },
    [pscustomobject]@{ Plugin = "TRAutoloader"; Check = "Placeable.GetCurrentTavernZone"; Found = Test-MemberByName -TypeName "Placeable" -MemberName "GetCurrentTavernZone" -MemberKind "Method" },
    [pscustomobject]@{ Plugin = "TRAutoloader"; Check = "ItemContainer.bigContainer"; Found = Test-MemberByName -TypeName "ItemContainer" -MemberName "bigContainer" -MemberKind "Field" },
    [pscustomobject]@{ Plugin = "TRAutoloader"; Check = "Food.halloweenFood"; Found = Test-MemberByName -TypeName "Food" -MemberName "halloweenFood" -MemberKind "Field" },
    [pscustomobject]@{ Plugin = "TRAutoloader"; Check = "HalloweenEvent type"; Found = ($null -ne $assembly.GetType("HalloweenEvent")) },
    [pscustomobject]@{ Plugin = "TRAutoloader"; Check = "DrinkDispenser container-updated field"; Found = Test-MemberByName -TypeName "DrinkDispenser" -MemberName "DrinkDispenserContainerUpdated" -MemberKind "Field" },
    [pscustomobject]@{ Plugin = "TRAutoloader"; Check = "OnlineManager.PlayingOnline"; Found = Test-MemberByName -TypeName "OnlineManager" -MemberName "PlayingOnline" -MemberKind "Method" },
    [pscustomobject]@{ Plugin = "TRAutoloader"; Check = "OnlineManager.IsMasterClient"; Found = Test-MemberByName -TypeName "OnlineManager" -MemberName "IsMasterClient" -MemberKind "Method" },
    [pscustomobject]@{ Plugin = "TRAutoloader"; Check = "OnlineSlotsManager.instance"; Found = Test-MemberByName -TypeName "OnlineSlotsManager" -MemberName "instance" -MemberKind "Field" },
    [pscustomobject]@{ Plugin = "TRAutoloader"; Check = "OnlineSlotsManager.SendSlot"; Found = Test-MemberByName -TypeName "OnlineSlotsManager" -MemberName "SendSlot" -MemberKind "Method" }
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
