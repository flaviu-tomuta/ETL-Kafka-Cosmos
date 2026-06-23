#!/bin/bash
# run-pipeline.sh
# Orchestrates the full agentic development pipeline.
# Requires Claude Code (claude CLI) to be installed and authenticated.
#
# Usage:
#   ./run-pipeline.sh                        # run full pipeline from story generation
#   ./run-pipeline.sh --from-story 3         # start from a specific story number
#   ./run-pipeline.sh --stories-only         # run Agent 1 only
#   ./run-pipeline.sh --branch my-branch     # use a specific feature branch name

set -euo pipefail

# ── colours ──────────────────────────────────────────────────────────────────
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
PURPLE='\033[0;35m'
NC='\033[0m'

# ── config ────────────────────────────────────────────────────────────────────
ARCH_DOC="docs/architecture-recap.md"
STORIES_FILE="pipeline/stories.md"
CODING_LOG="pipeline/coding-log.md"
QC_REPORT="pipeline/qc-report.md"
PUSH_LOG="pipeline/push-log.md"
MAX_QC_ITERATIONS=3
START_FROM_STORY=${FROM_STORY:-1}
STORIES_ONLY=false
FEATURE_BRANCH=""          # set by --branch arg or auto-generated in setup_feature_branch
MAIN_BRANCH="main"         # branch to merge into — PRs target this

# ── arg parsing ───────────────────────────────────────────────────────────────
while [[ $# -gt 0 ]]; do
  case $1 in
    --from-story) START_FROM_STORY="$2"; shift 2 ;;
    --stories-only) STORIES_ONLY=true; shift ;;
    --branch) FEATURE_BRANCH="$2"; shift 2 ;;
    *) echo "Unknown argument: $1"; exit 1 ;;
  esac
done

# ── helpers ───────────────────────────────────────────────────────────────────
log()        { echo -e "${BLUE}[pipeline]${NC} $1"; }
log_agent()  { echo -e "${PURPLE}[agent]${NC} $1"; }
log_human()  { echo -e "${YELLOW}[human]${NC} $1"; }
log_ok()     { echo -e "${GREEN}[ok]${NC} $1"; }
log_error()  { echo -e "${RED}[error]${NC} $1"; }

pause_for_human() {
  local message=$1
  echo ""
  echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
  log_human "HUMAN REVIEW REQUIRED"
  log_human "$message"
  echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
  echo ""
  read -p "Press [a] to approve and continue, [r] to revise (sends feedback to agent), or [q] to quit: " choice
  case $choice in
    a|A) log_ok "Approved — continuing pipeline"; return 0 ;;
    r|R)
      echo ""
      read -p "Enter your feedback for the agent: " feedback
      echo "$feedback"  # caller captures this
      return 1
      ;;
    q|Q) log "Pipeline paused by user. Re-run with --from-story to resume."; exit 0 ;;
    *) log_error "Invalid choice. Defaulting to approve."; return 0 ;;
  esac
}

check_prerequisites() {
  log "Checking prerequisites..."

  # Claude CLI
  if ! command -v claude &> /dev/null; then
    log_error "Claude CLI not found. Install via: npm install -g @anthropic-ai/claude-code"
    exit 1
  fi

  # Architecture document
  if [ ! -f "$ARCH_DOC" ]; then
    log_error "Architecture document not found at $ARCH_DOC"
    log_error "Expected at: $ARCH_DOC"
    exit 1
  fi

  # Git repository
  if ! git rev-parse --git-dir &> /dev/null; then
    log_error "Not inside a git repository."
    log_error "The pipeline must be run from inside a cloned repo so Agent 4 knows where to push."
    log_error "Run: git clone <your-repo-url> && cd <repo-name>"
    exit 1
  fi

  # Git remote
  if ! git remote get-url origin &> /dev/null; then
    log_error "No git remote named 'origin' found."
    log_error "Add one with: git remote add origin <your-repo-url>"
    exit 1
  fi

  # Git user config
  if [ -z "$(git config user.name)" ] || [ -z "$(git config user.email)" ]; then
    log_error "Git user not configured. Agent 4 needs this to commit."
    log_error "Run:"
    log_error "  git config user.name  'Your Name'"
    log_error "  git config user.email 'you@example.com'"
    exit 1
  fi

  # Show repo info so user can confirm before pipeline runs
  local remote_url branch repo_name
  remote_url=$(git remote get-url origin)
  branch=$(git rev-parse --abbrev-ref HEAD)
  repo_name=$(basename "$(git rev-parse --show-toplevel)")

  echo ""
  log "Git repository confirmed:"
  log "  Repo   : $repo_name"
  log "  Remote : $remote_url"
  log "  Branch : $branch"
  echo ""

  read -p "Agent 4 will push to the repository above. Continue? [y/n]: " confirm
  if [[ "$confirm" != "y" && "$confirm" != "Y" ]]; then
    log "Pipeline cancelled."
    exit 0
  fi

  mkdir -p pipeline src tests
  log_ok "Prerequisites OK"
}

setup_feature_branch() {
  # Generate branch name if not provided via --branch
  if [ -z "$FEATURE_BRANCH" ]; then
    local timestamp
    timestamp=$(date +"%Y%m%d-%H%M%S")
    FEATURE_BRANCH="feature/pipeline-run-${timestamp}"
  fi

  # Check if branch already exists locally (resuming a run)
  if git show-ref --verify --quiet "refs/heads/${FEATURE_BRANCH}"; then
    log "Resuming on existing branch: $FEATURE_BRANCH"
    git checkout "$FEATURE_BRANCH"
  else
    log "Creating feature branch: $FEATURE_BRANCH"
    git checkout -b "$FEATURE_BRANCH"
  fi

  # Save branch name to pipeline dir so it survives resume
  echo "$FEATURE_BRANCH" > pipeline/.feature-branch
  log_ok "Feature branch ready: $FEATURE_BRANCH"
  log "All commits will be pushed to this branch."
  log "When the pipeline completes, open a PR from $FEATURE_BRANCH → $MAIN_BRANCH."
  echo ""
}

restore_feature_branch() {
  # When resuming, restore the branch name from the saved file
  if [ -f pipeline/.feature-branch ]; then
    FEATURE_BRANCH=$(cat pipeline/.feature-branch)
    log "Restored feature branch from previous run: $FEATURE_BRANCH"
    git checkout "$FEATURE_BRANCH" 2>/dev/null || {
      log_error "Could not checkout branch $FEATURE_BRANCH — it may not exist locally."
      log_error "Run: git fetch origin && git checkout $FEATURE_BRANCH"
      exit 1
    }
  fi
}

get_story_ids() {
  # Extract all STORY-N identifiers from stories.md in order
  grep -oE 'STORY-[0-9]+' "$STORIES_FILE" | grep -oE '[0-9]+' | sort -n | uniq
}

get_story_title() {
  local story_id=$1
  grep -A1 "STORY-${story_id}:" "$STORIES_FILE" | head -1 | sed "s/STORY-${story_id}: //"
}

get_qc_verdict() {
  # Read only the most recent verdict — first match from top of file
  # since new entries are prepended, the first Verdict line is always the latest
  grep -oE 'Verdict: (PASS|FAIL|ESCALATE)' "$QC_REPORT" | head -1 | sed 's/Verdict: //'
}

# Prepend a new QC entry to the top of qc-report.md
# so the latest verdict is always at the top and the file is never overwritten
prepend_qc_placeholder() {
  local story_id=$1
  local iteration=$2
  local timestamp
  timestamp=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
  local placeholder="<!-- QC-STORY-${story_id}-ITER-${iteration} | ${timestamp} | pending -->"

  if [ -f "$QC_REPORT" ]; then
    # File exists — prepend placeholder above existing content
    local existing
    existing=$(cat "$QC_REPORT")
    printf '%s

%s
' "$placeholder" "$existing" > "$QC_REPORT"
  else
    # First run — create the file
    echo "$placeholder" > "$QC_REPORT"
  fi
}

# ── AGENT 1 — story writer ────────────────────────────────────────────────────
run_agent_1() {
  log_agent "Agent 1 (story writer) starting..."

  local approved=false
  local feedback=""

  while [ "$approved" = false ]; do
    if [ -n "$feedback" ]; then
      log_agent "Re-running Agent 1 with feedback: $feedback"
      claude --print \
        "You previously generated stories for the architecture document. 
        The human reviewer has provided the following feedback — update 
        stories.md accordingly and re-check the coverage checklist:
        
        Feedback: $feedback
        
        Re-read docs/architecture-recap.md and pipeline/stories.md, 
        apply the feedback, and overwrite pipeline/stories.md with 
        the revised stories." \
        --allowedTools "Read,Write" \
        2>&1 | tee pipeline/agent1.log
    else
      log_agent "Reading architecture document and generating stories..."
      claude --print \
        "Read the architecture document at docs/architecture-recap.md.
        Follow the /agent-story-writer instructions in CLAUDE.md exactly.
        Generate the complete story set and write it to pipeline/stories.md.
        Do not stop until the coverage checklist is fully ticked." \
        --allowedTools "Read,Write" \
        2>&1 | tee pipeline/agent1.log
    fi

    echo ""
    log "Stories written to $STORIES_FILE"
    log "Opening for review..."
    echo ""
    cat "$STORIES_FILE"
    echo ""

    local result
    if pause_for_human "Review pipeline/stories.md — check coverage checklist is complete, stories are in dependency order, and all failure scenarios are covered."; then
      approved=true
    else
      feedback=$(pause_for_human "Review pipeline/stories.md — check coverage checklist is complete, stories are in dependency order, and all failure scenarios are covered." 2>&1 | tail -1)
    fi
  done

  log_ok "Stories approved — moving to Agent 2"
}

# ── AGENT 2 — coder ───────────────────────────────────────────────────────────
run_agent_2() {
  local story_id=$1
  local story_title
  story_title=$(get_story_title "$story_id")

  log_agent "Agent 2 (coder) — implementing STORY-${story_id}: ${story_title}"

  claude --print \
    "Follow the /agent-coder instructions in CLAUDE.md exactly.
    Implement STORY-${story_id} from pipeline/stories.md.
    
    Rules:
    - Write the test first (TDD — failing test before production code)
    - Write only the minimum code to make tests pass
    - Use exact class names from docs/architecture-recap.md
    - Register any new services in the DI container
    - Update pipeline/coding-log.md with your summary when done
    
    Read these files before starting:
    - pipeline/stories.md (find STORY-${story_id})
    - docs/architecture-recap.md
    - pipeline/coding-log.md (see what has already been built)" \
    --allowedTools "Read,Write,Bash" \
    2>&1 | tee pipeline/agent2-story${story_id}.log

  log_ok "STORY-${story_id} coding complete"
}

# ── AGENT 3 — QC ─────────────────────────────────────────────────────────────
run_agent_3() {
  local story_id=$1
  local iteration=$2
  local story_title
  story_title=$(get_story_title "$story_id")

  log_agent "Agent 3 (QC) — checking STORY-${story_id} iteration ${iteration}/${MAX_QC_ITERATIONS}"

  # Prepend a placeholder at the top of qc-report.md before the agent runs.
  # The agent replaces this placeholder with its actual report.
  # This avoids any create-vs-append confusion — the file always exists
  # and the agent always writes at the top.
  prepend_qc_placeholder "$story_id" "$iteration"

  local max_retries=3
  local attempt=1
  local verdict=""

  while [ "$attempt" -le "$max_retries" ]; do
    log_agent "QC write attempt ${attempt}/${max_retries} for STORY-${story_id} iteration ${iteration}"

    claude \
      "Follow the /agent-qc instructions in CLAUDE.md exactly.
      Review the code produced for STORY-${story_id}.
      This is QC iteration ${iteration} of ${MAX_QC_ITERATIONS}.

      Read these files:
      - pipeline/stories.md (find STORY-${story_id} acceptance criteria)
      - pipeline/coding-log.md (files produced for this story)
      - docs/architecture-recap.md (verify class names and patterns)
      - All src/ and tests/ files listed in the coding log for this story

      IMPORTANT — writing your report:
      - pipeline/qc-report.md already exists and already has content
      - Read the current content of pipeline/qc-report.md first
      - Find the placeholder line: <!-- QC-STORY-${story_id}-ITER-${iteration} | ... | pending -->
      - Replace that placeholder line with your full QC report for this story
      - Do NOT delete or overwrite any other entries already in the file
      - The file must contain all previous QC entries plus your new one at the top

      If iteration ${MAX_QC_ITERATIONS} and issues remain, set verdict to ESCALATE." \
      --allowedTools "Read,Write" \
      2>&1 | tee pipeline/agent3-story${story_id}-iter${iteration}-attempt${attempt}.log

    # Verify the placeholder was replaced — if it still says "pending" the write failed
    if grep -q "<!-- QC-STORY-${story_id}-ITER-${iteration}.*pending -->" "$QC_REPORT"; then
      log_error "QC report write failed (placeholder still present) — retrying (${attempt}/${max_retries})"
      attempt=$((attempt + 1))
      sleep 2
    else
      verdict=$(get_qc_verdict)
      if [ -n "$verdict" ]; then
        log_ok "QC report written successfully — verdict: $verdict"
        break
      else
        log_error "QC verdict not found in report — retrying (${attempt}/${max_retries})"
        attempt=$((attempt + 1))
        sleep 2
      fi
    fi
  done

  if [ "$attempt" -gt "$max_retries" ]; then
    log_error "QC agent failed to write report after ${max_retries} attempts for STORY-${story_id}"
    pause_for_human "Agent 3 could not write pipeline/qc-report.md for STORY-${story_id}. Check the log at pipeline/agent3-story${story_id}-iter${iteration}-attempt${max_retries}.log and write the verdict manually before continuing."
    verdict=$(get_qc_verdict)
  fi

  echo "$verdict"
}

# ── AGENT 4 — git push ────────────────────────────────────────────────────────
run_agent_4() {
  local story_id=$1
  local story_title
  story_title=$(get_story_title "$story_id")

  local remote_url repo_name
  remote_url=$(git remote get-url origin)
  repo_name=$(basename "$(git rev-parse --show-toplevel)")

  log_agent "Agent 4 (git push) — committing STORY-${story_id} to ${repo_name} (${FEATURE_BRANCH})"

  claude \
    "Follow the /agent-git-push instructions in CLAUDE.md exactly.

    Repository context:
    - Remote URL    : $remote_url
    - Feature branch: $FEATURE_BRANCH
    - Target branch : $MAIN_BRANCH (do NOT push to this — feature branch only)
    - Repo name     : $repo_name

    Before doing anything:
    1. Read pipeline/qc-report.md and confirm Verdict is PASS
    2. If verdict is not PASS or is ESCALATE, stop immediately and report an error — do not push

    If PASS:
    - Confirm you are on branch $FEATURE_BRANCH before staging anything:
        git rev-parse --abbrev-ref HEAD
      If not on $FEATURE_BRANCH, run: git checkout $FEATURE_BRANCH
    - Stage only the files listed in pipeline/coding-log.md for STORY-${story_id}
    - Commit with the required message format from CLAUDE.md
    - Push to the feature branch ONLY: git push origin $FEATURE_BRANCH
    - Do NOT push to $MAIN_BRANCH under any circumstances
    - Append the result to pipeline/push-log.md including the remote URL and commit hash" \
    --allowedTools "Read,Write,Bash" \
    2>&1 | tee pipeline/agent4-story${story_id}.log

  # Verify the push actually happened
  local last_push
  last_push=$(tail -1 pipeline/push-log.md 2>/dev/null || echo "")
  if echo "$last_push" | grep -q "STORY-${story_id}"; then
    log_ok "STORY-${story_id} pushed to $remote_url ($FEATURE_BRANCH)"
  else
    log_error "Push log does not confirm STORY-${story_id} was pushed. Check pipeline/agent4-story${story_id}.log"
    pause_for_human "Agent 4 may not have pushed STORY-${story_id} successfully. Review the log and push manually if needed."
  fi
}

# ── story loop ────────────────────────────────────────────────────────────────
process_story() {
  local story_id=$1
  local story_title
  story_title=$(get_story_title "$story_id")

  echo ""
  log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
  log "Processing STORY-${story_id}: ${story_title}"
  log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

  local code_approved=false

  while [ "$code_approved" = false ]; do

    # Agent 2 — code the story
    run_agent_2 "$story_id"

    echo ""
    log "Code for STORY-${story_id} written. Review before QC..."
    echo ""

    # Human review 2 — code review
    if pause_for_human "Review the code for STORY-${story_id} in src/ and tests/. Check it looks correct before QC runs."; then
      code_approved=true
    else
      log "Sending back to Agent 2 for rework..."
      code_approved=false
    fi
  done

  # QC loop — max 3 iterations
  local qc_iteration=1
  local qc_passed=false

  while [ "$qc_passed" = false ] && [ "$qc_iteration" -le "$MAX_QC_ITERATIONS" ]; do

    local verdict
    verdict=$(run_agent_3 "$story_id" "$qc_iteration")

    echo ""
    log "QC report for STORY-${story_id} iteration ${qc_iteration}:"
    cat "$QC_REPORT"
    echo ""

    case $verdict in
      PASS)
        log_ok "QC PASS — proceeding to git push"
        qc_passed=true
        ;;
      FAIL)
        if [ "$qc_iteration" -lt "$MAX_QC_ITERATIONS" ]; then
          log "QC FAIL — sending back to Agent 2 (iteration $((qc_iteration + 1))/${MAX_QC_ITERATIONS})"
          run_agent_2 "$story_id"
          qc_iteration=$((qc_iteration + 1))
        else
          log_error "QC FAIL after ${MAX_QC_ITERATIONS} iterations — escalating to human"
          pause_for_human "QC failed after ${MAX_QC_ITERATIONS} attempts on STORY-${story_id}. Review pipeline/qc-report.md and decide how to proceed."
          qc_passed=true  # human took over — proceed
        fi
        ;;
      ESCALATE)
        log_error "QC ESCALATE — human review required for STORY-${story_id}"
        pause_for_human "Agent 3 escalated STORY-${story_id} after ${MAX_QC_ITERATIONS} iterations. Review pipeline/qc-report.md."
        qc_passed=true
        ;;
      *)
        log_error "Could not determine QC verdict. Check pipeline/qc-report.md manually."
        pause_for_human "QC verdict unclear for STORY-${story_id}. Review pipeline/qc-report.md."
        qc_passed=true
        ;;
    esac
  done

  # Agent 4 — git push
  run_agent_4 "$story_id"
  log_ok "STORY-${story_id} complete and pushed"
}

# ── main ──────────────────────────────────────────────────────────────────────
main() {
  echo ""
  log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
  log "Agentic development pipeline"
  log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
  echo ""

  check_prerequisites

  # Agent 1 — generate stories (skip if resuming)
  if [ "$START_FROM_STORY" -eq 1 ] && [ ! -f "$STORIES_FILE" ]; then
    run_agent_1
  elif [ "$START_FROM_STORY" -eq 1 ] && [ -f "$STORIES_FILE" ]; then
    log "Stories file already exists. Skipping Agent 1."
    log "Delete pipeline/stories.md to regenerate, or use --from-story to resume."
  fi

  if [ "$STORIES_ONLY" = true ]; then
    log "Stories-only mode — pipeline complete."
    exit 0
  fi

  # Set up or restore feature branch before any code is committed
  if [ "$START_FROM_STORY" -gt 1 ] && [ -f pipeline/.feature-branch ]; then
    restore_feature_branch
  else
    setup_feature_branch
  fi

  # Process each story in order
  for story_id in $(get_story_ids); do
    if [ "$story_id" -lt "$START_FROM_STORY" ]; then
      log "Skipping STORY-${story_id} (resuming from STORY-${START_FROM_STORY})"
      continue
    fi
    process_story "$story_id"
  done

  # Pipeline complete — print summary and PR instructions
  local remote_url
  remote_url=$(git remote get-url origin)

  echo ""
  log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
  log_ok "Pipeline complete — all stories implemented and pushed"
  log "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
  echo ""
  cat "$PUSH_LOG"
  echo ""
  echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
  log_human "NEXT STEP — open a pull request"
  log_human "Branch : $FEATURE_BRANCH"
  log_human "Target : $MAIN_BRANCH"
  log_human "Remote : $remote_url"
  echo ""
  log_human "Review the branch on GitHub then open a PR to merge into $MAIN_BRANCH."
  log_human "The CI pipeline will run automatically when the PR is opened."
  echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
  echo ""
}

main "$@"
