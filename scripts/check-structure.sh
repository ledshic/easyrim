#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT_DIR"

exit_code=0

pass() {
  printf 'PASS: %s\n' "$1"
}

fail() {
  printf 'FAIL: %s\n' "$1"
  exit_code=1
}

ensure_dir_exists() {
  local path="$1"
  if [[ -d "$path" ]]; then
    pass "Directory exists: $path"
  else
    fail "Missing required directory: $path"
  fi
}

ensure_dir_absent() {
  local path="$1"
  if [[ -d "$path" ]]; then
    fail "Legacy directory still present: $path"
  else
    pass "Legacy directory absent: $path"
  fi
}

ensure_no_match() {
  local pattern="$1"
  local scope="$2"
  local label="$3"

  local files=()
  if command -v rg >/dev/null 2>&1; then
    while IFS= read -r line; do
      files+=("$line")
    done < <(rg --files "$scope" -g "*.md" -g "*.xml" -g "*.cs" -g "*.csproj" -g "*.props" -g "*.sln")
  else
    while IFS= read -r line; do
      files+=("$line")
    done < <(find "$scope" -type f \( -name "*.md" -o -name "*.xml" -o -name "*.cs" -o -name "*.csproj" -o -name "*.props" -o -name "*.sln" \))
  fi

  if (( ${#files[@]} == 0 )); then
    pass "$label"
    return
  fi

  if command -v rg >/dev/null 2>&1; then
    if rg -n "$pattern" "${files[@]}" >/dev/null 2>&1; then
      fail "$label"
      rg -n "$pattern" "${files[@]}" || true
    else
      pass "$label"
    fi
  else
    if grep -n -E "$pattern" "${files[@]}" >/dev/null 2>&1; then
      fail "$label"
      grep -n -E "$pattern" "${files[@]}" || true
    else
      pass "$label"
    fi
  fi
}

printf 'Checking Defs directory normalization...\n'
ensure_dir_exists "Defs/AbilityDef"
ensure_dir_exists "Defs/HediffDef"
ensure_dir_exists "Defs/RecipeDef"
ensure_dir_exists "Defs/ThingDef"
ensure_dir_exists "Defs/ThoughtDef"

ensure_dir_absent "Defs/AbilityDefs"
ensure_dir_absent "Defs/HediffDefs"
ensure_dir_absent "Defs/RecipeDefs"
ensure_dir_absent "Defs/ThingDefs"
ensure_dir_absent "Defs/ThoughtDefs"

printf '\nChecking DefInjected subfolder normalization...\n'
for lang in English ChineseSimplified; do
  base="Languages/$lang/DefInjected"
  ensure_dir_exists "$base"
  ensure_dir_exists "$base/AbilityDef"
  ensure_dir_exists "$base/HediffDef"
  ensure_dir_exists "$base/RecipeDef"
  ensure_dir_exists "$base/ThingDef"
  ensure_dir_exists "$base/ThoughtDef"
  ensure_dir_exists "$base/TraitDef"

  ensure_dir_absent "$base/AbilityDefs"
  ensure_dir_absent "$base/HediffDefs"
  ensure_dir_absent "$base/RecipeDefs"
  ensure_dir_absent "$base/ThingDefs"
  ensure_dir_absent "$base/ThoughtDefs"
  ensure_dir_absent "$base/TraitDefs"
done

printf '\nChecking patch layout...\n'
ensure_dir_exists "Patches/ThingDef"
if compgen -G "Patches/*.xml" >/dev/null; then
  fail "Flat patch XML files found under Patches/; move them into Patches/<DefType>/"
  ls -1 Patches/*.xml
else
  pass "No flat patch XML files under Patches/"
fi

printf '\nChecking forbidden spellings and legacy paths in docs/xml...\n'
ensure_no_match "Receipes_" "." "No Receipes_ typo remains"
ensure_no_match "Defs/(AbilityDefs|HediffDefs|RecipeDefs|ThingDefs|ThoughtDefs)" "." "No legacy plural Def paths remain in text files"
ensure_no_match "DefInjected/(AbilityDefs|HediffDefs|RecipeDefs|ThingDefs|ThoughtDefs|TraitDefs)" "." "No legacy plural DefInjected paths remain in text files"

printf '\nStructure check complete.\n'
exit "$exit_code"
