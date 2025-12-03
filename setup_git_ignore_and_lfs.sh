#!/usr/bin/env bash
set -euo pipefail

# === CONFIG ===
PROJECT_ROOT="/home/anons/Desktop/rumble23nov"
REPO_MSG="Apply .gitignore, .gitattributes and stop tracking large asset folders (auto)"
GIT_AUTHOR_NAME="${GIT_AUTHOR_NAME:-$(git config user.name || echo "dev")}"
GIT_AUTHOR_EMAIL="${GIT_AUTHOR_EMAIL:-$(git config user.email || echo "dev@example.com")}"

# Folders to exclude (relative to project root). Keep exact entries below.
EXCLUDE_FOLDERS=(
  "Assets/Procedural Worlds"
  "Assets/Fantasy Treasure Chest_Standard"
  "Assets/Invector-3rdPersonController"
  "Assets/Invector-AIController"
  "Assets/Invector-FSMAIController"
  "Assets/_downloadedassets"
  "Assets/Rumble/RumbleFBX"
)

# File patterns to store in Git LFS
LFS_PATTERNS=(
  "*.png"
  "*.jpg"
  "*.tga"
  "*.psd"
  "*.fbx"
  "*.obj"
  "*.wav"
  "*.mp3"
  "*.ogg"
  "*.mp4"
  "*.mov"
  "*.exr"
  "*.hdr"
  "*.zip"
)

# === SCRIPT ===
echo "Project root: $PROJECT_ROOT"
if [ ! -d "$PROJECT_ROOT" ]; then
  echo "ERROR: project root does not exist: $PROJECT_ROOT"
  exit 1
fi

cd "$PROJECT_ROOT"

# Initialize git if needed
if [ ! -d ".git" ]; then
  echo "No .git found — initializing repository..."
  git init
else
  echo "Git repository found."
fi

# Create .gitignore
cat > .gitignore << 'EOF'
# Unity standard ignores
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
[Bb]uilds/
[Ll]ogs/
UserSettings/
MemoryCaptures/
.vscode/
.idea/
*.suo
*.user
*.userprefs
*.pidb
*.booproj
*.svd
*.pdb
*.mdb
*.opendb
*.VC.db
*.sln
*.csproj
*.unityproj
*.tmp
*.temp
*.bak
*.orig
*.cache

# OS
.DS_Store
Thumbs.db

# Rider / JetBrains
.idea/
*.sublime-workspace
*.sublime-project

# Ignore per-project generated Addressables build outputs and SBP caches
/Library/com.unity.addressables/
/Library/com.unity.scriptablebuildpipeline/
/ServerData/
/AddressablesPlayerData/

# Keep Addressables asset settings tracked (do NOT ignore the core settings)
# Assets/AddressableAssetsData/AddressableAssetSettings.asset should remain tracked.

# Ignore build output folders
/Build/
/Builds/

# ignore Node / npm if present
node_modules/

# Local/debug/temp folders
/Assets/TempAssets/
/Assets/StreamingAssets/Generated/

# allow local OS cache (user's home)
~/.cache/unity3d/

# End of .gitignore
EOF

# Append the user-specified heavy folders into .gitignore
echo "" >> .gitignore
echo "# Exclude large 3rd-party asset folders (project-specific)" >> .gitignore
for f in "${EXCLUDE_FOLDERS[@]}"; do
  # ensure path slash on the left so it's anchored to repo root
  echo "/$f/" >> .gitignore
done

echo ".gitignore created/updated. Please review it before committing."

# Create .gitattributes for LFS defaults
cat > .gitattributes << 'EOF'
# Use Git LFS for common large binary asset types
*.png filter=lfs diff=lfs merge=lfs -text
*.jpg filter=lfs diff=lfs merge=lfs -text
*.tga filter=lfs diff=lfs merge=lfs -text
*.psd filter=lfs diff=lfs merge=lfs -text
*.fbx filter=lfs diff=lfs merge=lfs -text
*.obj filter=lfs diff=lfs merge=lfs -text
*.wav filter=lfs diff=lfs merge=lfs -text
*.mp3 filter=lfs diff=lfs merge=lfs -text
*.ogg filter=lfs diff=lfs merge=lfs -text
*.mp4 filter=lfs diff=lfs merge=lfs -text
*.mov filter=lfs diff=lfs merge=lfs -text
*.exr filter=lfs diff=lfs merge=lfs -text
*.hdr filter=lfs diff=lfs merge=lfs -text
*.zip filter=lfs diff=lfs merge=lfs -text
EOF

echo ".gitattributes created."

# Initialize Git LFS and track patterns
if ! command -v git-lfs >/dev/null 2>&1; then
  echo "Git LFS is not installed on your system. Installing via 'git lfs install' will be attempted, but you may need to install git-lfs package manually."
fi

echo "Running git lfs install..."
git lfs install --skip-repo || true

for p in "${LFS_PATTERNS[@]}"; do
  echo "git lfs track \"$p\""
  git lfs track "$p" || true
done

# Ensure .gitattributes is added
git add .gitattributes

# Add .gitignore
git add .gitignore

# Stop tracking the specified large folders if they are already tracked in the index
echo "Removing specified large folders from git index (will remain on disk)..."
for f in "${EXCLUDE_FOLDERS[@]}"; do
  if git ls-files --error-unmatch -- "$f" >/dev/null 2>&1 || git ls-files --error-unmatch -- "$f/" >/dev/null 2>&1; then
    echo " - Removing from index: $f"
    # remove tracked files in that folder from the index but keep them on disk
    git rm -r --cached --ignore-unmatch -- "$f"
  else
    echo " - Not tracked / nothing to remove for: $f"
  fi
done

# Stage any remaining changes (new/modified assets, .gitignore, .gitattributes)
git add -A

# Commit
echo "Committing changes..."
git commit -m "$REPO_MSG" --author="$GIT_AUTHOR_NAME <$GIT_AUTHOR_EMAIL>" || echo "Nothing to commit (maybe already committed)."

echo "Done. Summary:"
echo " - .gitignore created at: $PROJECT_ROOT/.gitignore"
echo " - .gitattributes created at: $PROJECT_ROOT/.gitattributes"
echo " - Git LFS tracking patterns were added."
echo " - The listed heavy folders were removed from the index (if they were previously tracked). They remain on disk."

echo ""
echo "Next steps:"
echo "1) Inspect .gitignore and .gitattributes and adjust if needed."
echo "2) If you haven't set a remote, add it: git remote add origin <your-repo-url>"
echo "3) Push to remote: git push -u origin main"
echo ""
echo "If the folders were previously pushed to remote and you need them removed from repository history (to shrink repo size), tell me and I will give you the git-filter-repo or BFG commands (careful: rewriting history)."

