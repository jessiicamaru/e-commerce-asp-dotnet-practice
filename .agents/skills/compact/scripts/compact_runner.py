import os
import subprocess
import json
import datetime
from pathlib import Path

def run_command(command, cwd=None):
    try:
        result = subprocess.run(
            command, 
            cwd=cwd, 
            shell=True, 
            capture_output=True, 
            encoding='utf-8', 
            check=True
        )
        return result.stdout.strip()
    except subprocess.CalledProcessError as e:
        return f"Error executing '{command}': {e.stderr.strip()}"

def gather_todos():
    todos = []
    output = run_command('git grep -I -n --untracked -E "TODO|FIXME"')
    if output and not output.startswith("Error"):
        for line in output.split('\n'):
            if line.strip():
                todos.append(line.strip())
    return todos

def load_state(state_file: Path) -> dict:
    """Load merged state file. Returns dict keyed by branch name."""
    if not state_file.exists():
        return {}
    try:
        with open(state_file, 'r', encoding='utf-8') as f:
            return json.load(f)
    except Exception:
        return {}

def save_state(state_file: Path, all_states: dict):
    """Save merged state file, merging any old branch-specific files."""
    # Auto-migrate: absorb any legacy .walkthrough_state_<branch>.json files
    parent = state_file.parent
    for legacy in parent.glob(".walkthrough_state_*.json"):
        if legacy.name == state_file.name:
            continue
        try:
            with open(legacy, 'r', encoding='utf-8') as f:
                legacy_data = json.load(f)
            branch_key = legacy_data.get("branch")
            if branch_key and branch_key not in all_states:
                all_states[branch_key] = legacy_data
            legacy.unlink()  # Delete old file after absorbing
            print(f"  Migrated and removed legacy state file: {legacy.name}")
        except Exception:
            pass

    with open(state_file, 'w', encoding='utf-8') as f:
        json.dump(all_states, f, indent=2)

def main():
    print("Gathering Compact Context...")
    context = {}

    # 1. Get current branch
    branch = run_command("git branch --show-current")
    if "Error" in branch or not branch:
        git_dir = run_command("git rev-parse --git-dir")
        if not git_dir or "Error" in git_dir:
            git_dir = ".git"
        rebase_merge_head = os.path.join(git_dir, "rebase-merge", "head-name")
        rebase_apply_head = os.path.join(git_dir, "rebase-apply", "head-name")
        if os.path.exists(rebase_merge_head):
            with open(rebase_merge_head, "r", encoding="utf-8") as f:
                branch = f.read().strip().replace("refs/heads/", "")
        elif os.path.exists(rebase_apply_head):
            with open(rebase_apply_head, "r", encoding="utf-8") as f:
                branch = f.read().strip().replace("refs/heads/", "")
        else:
            status = run_command("git status")
            if "rebasing branch" in status:
                import re
                match = re.search(r"rebasing branch '([^']+)'", status)
                if match:
                    branch = match.group(1)
    if not branch or "Error" in branch:
        print(json.dumps({"error": "Not a git repository or no branch found."}))
        return

    context['branch'] = branch
    context['current_commit'] = run_command("git rev-parse HEAD")
    context['timestamp'] = datetime.datetime.now().isoformat()

    output_dir = Path("docs/project-walkthrough")
    output_dir.mkdir(parents=True, exist_ok=True)

    # Unified state file for ALL branches
    state_file = output_dir / ".walkthrough_state.json"
    all_states = load_state(state_file)
    branch_state = all_states.get(branch, {})

    if not branch_state:
        context['mode'] = "INITIALIZATION"
        context['message'] = f"No state found for branch '{branch}'. Running full initialization."
    else:
        context['mode'] = "UPDATE"
        last_commit = branch_state.get('last_commit_id')
        context['last_commit_id'] = last_commit

        if last_commit:
            log_cmd = f'git log {last_commit}..HEAD --pretty=format:"%h - %an: %s"'
            context['git_log'] = run_command(log_cmd)

            stat_cmd = f'git diff {last_commit} HEAD --stat'
            diff_stat = run_command(stat_cmd)
            context['git_diff_stat'] = diff_stat

            lines_changed = 0
            for line in diff_stat.split('\n'):
                if "files changed" in line:
                    parts = line.split(',')
                    for p in parts:
                        if "insertions" in p or "deletions" in p:
                            try:
                                lines_changed += int(p.strip().split(' ')[0])
                            except ValueError:
                                pass

            context['total_lines_changed'] = lines_changed
            if lines_changed < 1000:
                diff_cmd = f'git diff {last_commit} HEAD -w'
                context['git_diff'] = run_command(diff_cmd)
            else:
                context['git_diff'] = "DIFF TOO LARGE. Rely on git log and stat."

    # Gather TODOs
    context['todos'] = gather_todos()

    # Output context JSON for agent to read
    output_file = output_dir / "compact_context.json"
    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(context, f, indent=2)

    print(f"\nSuccessfully generated {output_file}.")
    print("Agent can now read 'compact_context.json' to update the walkthrough.")
    print(f"\nState file location: {state_file}  (unified, all branches in one file)")

if __name__ == "__main__":
    main()
