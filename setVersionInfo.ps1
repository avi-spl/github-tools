
$rootDir = (resolve-path .\).Path;
$outDir = Join-Path $rootDir "build";

function TeamCity-SetBuildNumber([string]$buildNumber) {
	Write-Output "##teamcity[buildNumber '$buildNumber']"
}
function TeamCity-SetParameter([string]$key, [string]$value) {
	Write-Output "##teamcity[setParameter name='$key' value='$value']"	
}

function Get-GitCommitNumber {
    $gittag = Get-GitTag
    if ($gittag -match '-(\d+)-') {
        $gitnumber = $matches[1]
    } else {
        $gitnumber = 0
    }
    $gitnumber
}

function Get-GitVersion {
    $gittag = Get-GitTag
    if ($gittag -match '^[v|V](\d\.\d\.\d)-(\d+)-.*$') {
        $gitversion = $gittag -replace '^v(\d\.\d\.\d)-(\d+)-.*$', "`$1.`$2"
    } else {
        $gitversion = "0.0.0.0"
    }
    $gitversion
}

function Get-GitTag {
	param
	(
		[string]$match = "",
		[bool]$always = $false
	)
	if($match -ne "") {
		if($always) {
			(& git describe --long --match "$match" --always)
		} else {
			(& git describe --long --match "$match")
		}
	} else {
		(& git describe --long)
	}
}

function Get-GitCommit {
    (& git log -1 --pretty=format:%H)
}

function Get-GitBranch {
	(& git rev-parse --abbrev-ref HEAD)
}

function get-versionDetails {
    $branch = Get-GitBranch;
    $commit = Get-GitCommit;
    $fullVersion = Get-GitVersion;
    $fullVersion -match "(\d+)\.(\d+)\.(\d+)\.(\d+)" | out-null;

    $script:version = @{
        "major" = $matches[1];
        "minor" = $matches[2];
        "patch" = $matches[3];
        "revision" = $matches[4];
        "commit" = $commit;
        "assembly" = $fullVersion
    }
    return $version;
}

$isTeamCityBuild = if("$env:BUILD_NUMBER".length -gt 0) { $TRUE } else { $FALSE }

Write-Host "Getting version" -ForegroundColor Green;
$versionInfo = get-versionDetails;
if ($isTeamCityBuild) {
    $tc_build_number = "{0}/{1}" -f $env:BUILD_NUMBER, $versionInfo.assembly;
    TeamCity-SetBuildNumber $tc_build_number;
    TeamCity-SetParameter "build.VERSION_FULL" $versionInfo.assembly
    $octopus_release_version = "{0}+sha.{1}" -f $versionInfo.assembly, $versionInfo.commit.SubString(0, 8)
    TeamCity-SetParameter "build.VERSION_OCTOPUS" $octopus_release_version
}
Write-Host ("Assembly Version is {0}" -f $versionInfo.assembly);
Write-Host ("Based on commit {0}" -f $versionInfo.commit);

Write-Host "Copying meta info"
$dst = Join-Path (Join-Path $outDir "published") "meta"
if (Test-Path $dst) {
    rd $dst -rec -force | out-null
}
mkdir $dst | out-null
# create the commit.sha file
[IO.File]::WriteAllText("$dst\commit.sha", $versionInfo.commit)
