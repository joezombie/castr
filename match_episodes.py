#!/usr/bin/env python3
"""
Match playlist episode names to MP3 files using fuzzy string matching.
Preserves the order of the playlist while finding the best matching MP3 file for each entry.
"""

import argparse
import json
import logging
import os
import re
import shutil
from difflib import SequenceMatcher
from pathlib import Path

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    datefmt='%Y-%m-%d %H:%M:%S'
)
logger = logging.getLogger(__name__)


def extract_filename_from_ls_line(line):
    """Extract the filename from an ls -l output line."""
    # Match the path after the timestamp
    match = re.search(r'/mnt/user/.*\.mp3', line)
    if match:
        full_path = match.group(0)
        # Remove escaped spaces
        full_path = full_path.replace('\\', '')
        # Extract just the filename
        return Path(full_path).name
    return None


def extract_part_number(title):
    """Extract part number from title."""
    # Try to find "Part One", "Part Two", etc.
    part_words = {
        'one': 1, 'two': 2, 'three': 3, 'four': 4, 'five': 5, 'six': 6,
        'seven': 7, 'eight': 8, 'nine': 9, 'ten': 10
    }

    # Check for "Part X:" format
    match = re.search(r'^Part\s+(One|Two|Three|Four|Five|Six|Seven|Eight|Nine|Ten|\d+)[:：]', title, flags=re.IGNORECASE)
    if match:
        part = match.group(1).lower()
        return part_words.get(part, int(part) if part.isdigit() else None)

    # Check for "Pt X:" format
    match = re.search(r'^Pt\s+(\d+)[:：]', title, flags=re.IGNORECASE)
    if match:
        return int(match.group(1))

    return None


def normalize_title(title):
    """Normalize title for better matching."""
    # Remove " | BEHIND THE BASTARDS" suffix
    title = re.sub(r'\s*[｜|]\s*BEHIND THE BASTARDS\s*$', '', title, flags=re.IGNORECASE)

    # Normalize unicode characters
    title = title.replace('：', ':').replace('？', '?').replace('｜', '|')

    # Remove extra whitespace
    title = ' '.join(title.split())

    return title.strip()


def normalize_for_comparison(title):
    """Normalize title for comparison - removes part numbers."""
    # Remove common prefixes
    title = re.sub(r'^Part (One|Two|Three|Four|Five|Six|Seven|Eight|Nine|Ten|\d+):\s*', '', title, flags=re.IGNORECASE)
    title = re.sub(r'^Pt\s+\d+[:：]\s*', '', title, flags=re.IGNORECASE)

    return normalize_title(title)


def similarity_score(a, b):
    """Calculate similarity score between two strings."""
    # First check if part numbers match (if both have them)
    part_a = extract_part_number(a)
    part_b = extract_part_number(b)

    # If both have part numbers and they don't match, heavily penalize
    if part_a is not None and part_b is not None and part_a != part_b:
        # Still calculate base similarity for the episode name
        base_score = SequenceMatcher(None, normalize_for_comparison(a).lower(),
                                    normalize_for_comparison(b).lower()).ratio()
        # But heavily penalize mismatched part numbers
        return base_score * 0.3

    # Calculate similarity on full normalized titles (keeping part numbers)
    a_norm = normalize_title(a).lower()
    b_norm = normalize_title(b).lower()
    full_score = SequenceMatcher(None, a_norm, b_norm).ratio()

    # Also calculate similarity without part numbers (for the episode name itself)
    a_base = normalize_for_comparison(a).lower()
    b_base = normalize_for_comparison(b).lower()
    base_score = SequenceMatcher(None, a_base, b_base).ratio()

    # If part numbers match (or one/both don't have part numbers), use weighted average
    # Give more weight to full title match
    return 0.7 * full_score + 0.3 * base_score


def find_best_match(playlist_name, mp3_files, used_indices):
    """Find the best matching MP3 file for a playlist entry."""
    best_score = 0
    best_match = None
    best_idx = None

    for idx, mp3_file in enumerate(mp3_files):
        if idx in used_indices:
            continue

        # Remove .mp3 extension for comparison
        mp3_title = mp3_file.replace('.mp3', '')
        score = similarity_score(playlist_name, mp3_title)

        if score > best_score:
            best_score = score
            best_match = mp3_file
            best_idx = idx

    return best_match, best_score, best_idx


def get_full_path_for_mp3(mp3_filename, files1_path='files1.txt'):
    """Find the full path for an MP3 file from the files1.txt listing."""
    if not os.path.exists(files1_path):
        logger.error("File not found: %s", files1_path)
        return None
    
    try:
        with open(files1_path, 'r', encoding='utf-8') as f:
            for line in f:
                # Extract and unescape path for comparison
                match = re.search(r'/mnt/user/.*\.mp3', line)
                if match:
                    full_path = match.group(0).replace('\\', '')
                    # Check if the filename matches
                    if full_path.endswith(mp3_filename):
                        return full_path
    except IOError as e:
        logger.error("Failed to read %s: %s", files1_path, e)
        return None
    
    return None


def rename_files(json_path='matched_episodes.json', files1_path='files1.txt',
                 dry_run=True, pad_width=3):
    """Rename MP3 files to include order prefix based on matched_episodes.json."""
    logger.info("Loading matches from %s...", json_path)
    
    if not os.path.exists(json_path):
        logger.error("File not found: %s", json_path)
        return 0, 0, 1
    
    if not os.path.exists(files1_path):
        logger.error("File not found: %s", files1_path)
        return 0, 0, 1
    
    try:
        with open(json_path, 'r', encoding='utf-8') as f:
            matches = json.load(f)
    except (IOError, json.JSONDecodeError) as e:
        logger.error("Failed to read %s: %s", json_path, e)
        return 0, 0, 1

    logger.info("Found %d matched episodes", len(matches))

    if dry_run:
        logger.info("")
        logger.info("*** DRY RUN - No files will be renamed ***")
        logger.info("")
    else:
        logger.info("")
        logger.info("*** LIVE RUN - Files will be renamed ***")
        logger.info("")

    renamed_count = 0
    skipped_count = 0
    error_count = 0

    for match in matches:
        order = match['order']
        mp3_file = match['mp3_file']
        order_prefix = str(order).zfill(pad_width)

        # Check if file already has order prefix
        if re.match(r'^\d{' + str(pad_width) + r'}_', mp3_file):
            logger.info("SKIP: %s (already has order prefix)", mp3_file)
            skipped_count += 1
            continue

        # Get the full path
        full_path = get_full_path_for_mp3(mp3_file, files1_path)
        if not full_path:
            logger.error("Could not find path for %s", mp3_file)
            error_count += 1
            continue

        # Construct new filename
        directory = os.path.dirname(full_path)
        new_filename = f"{order_prefix}_{mp3_file}"
        new_path = os.path.join(directory, new_filename)

        logger.info("%3d. %s", order, mp3_file)
        logger.info("     → %s", new_filename)

        if not dry_run:
            try:
                if os.path.exists(full_path):
                    shutil.move(full_path, new_path)
                    logger.info("     ✓ Renamed successfully")
                    renamed_count += 1
                else:
                    logger.error("     ✗ Source file not found!")
                    error_count += 1
            except (IOError, OSError, PermissionError) as e:
                logger.error("     ✗ Error: %s", e)
                error_count += 1
        else:
            renamed_count += 1

    logger.info("=" * 80)
    logger.info("Summary:")
    if dry_run:
        logger.info("  Would rename: %d", renamed_count)
    else:
        logger.info("  Renamed: %d", renamed_count)
    logger.info("  Skipped (already prefixed): %d", skipped_count)
    logger.info("  Errors: %d", error_count)

    return renamed_count, skipped_count, error_count


def generate_rename_script(json_path='matched_episodes.json', files1_path='files1.txt',
                           output_path='rename_episodes.sh', pad_width=3):
    """Generate a bash script with rename commands."""
    logger.info("Loading matches from %s...", json_path)
    
    if not os.path.exists(json_path):
        logger.error("File not found: %s", json_path)
        return None
    
    if not os.path.exists(files1_path):
        logger.error("File not found: %s", files1_path)
        return None
    
    try:
        with open(json_path, 'r', encoding='utf-8') as f:
            matches = json.load(f)
    except (IOError, json.JSONDecodeError) as e:
        logger.error("Failed to read %s: %s", json_path, e)
        return None

    logger.info("Found %d matched episodes", len(matches))

    commands = ['#!/bin/bash', '', '# Rename MP3 files with order prefix', '']

    for match in matches:
        order = match['order']
        mp3_file = match['mp3_file']
        order_prefix = str(order).zfill(pad_width)

        # Skip if already has order prefix
        if re.match(r'^\d{' + str(pad_width) + r'}_', mp3_file):
            commands.append(f'# SKIP: {mp3_file} (already has order prefix)')
            continue

        full_path = get_full_path_for_mp3(mp3_file, files1_path)
        if not full_path:
            commands.append(f'# ERROR: Could not find path for {mp3_file}')
            continue

        directory = os.path.dirname(full_path)
        new_filename = f"{order_prefix}_{mp3_file}"
        new_path = os.path.join(directory, new_filename)

        # Escape single quotes in paths for bash
        escaped_src = full_path.replace("'", "'\\''")
        escaped_dst = new_path.replace("'", "'\\''")

        commands.append(f"mv '{escaped_src}' '{escaped_dst}'")

    try:
        with open(output_path, 'w', encoding='utf-8') as f:
            f.write('\n'.join(commands) + '\n')
    except IOError as e:
        logger.error("Failed to write %s: %s", output_path, e)
        return None

    logger.info("Rename script saved to %s", output_path)
    logger.info("Review the script, then run: bash %s", output_path)

    return output_path


def generate_map_file(json_path='matched_episodes.json', output_path='episode_order.txt'):
    """Generate a map file for the podcast feed API defining episode order."""
    logger.info("Loading matches from %s...", json_path)
    
    if not os.path.exists(json_path):
        logger.error("File not found: %s", json_path)
        return None
    
    try:
        with open(json_path, 'r', encoding='utf-8') as f:
            matches = json.load(f)
    except (IOError, json.JSONDecodeError) as e:
        logger.error("Failed to read %s: %s", json_path, e)
        return None

    logger.info("Found %d matched episodes", len(matches))

    # Sort by order and write filenames
    sorted_matches = sorted(matches, key=lambda m: m['order'])

    try:
        with open(output_path, 'w', encoding='utf-8') as f:
            for match in sorted_matches:
                f.write(match['mp3_file'] + '\n')
    except IOError as e:
        logger.error("Failed to write %s: %s", output_path, e)
        return None

    logger.info("Map file saved to %s", output_path)
    logger.info("Contains %d episodes in playlist order", len(sorted_matches))
    logger.info("\nUse this file with the PodcastFeedApi by setting MapFile in appsettings.json")

    return output_path


def reverse_list_file(input_path, output_path=None, in_place=False):
    """Reverse the order of lines in a list file (descending to ascending or vice versa).

    Args:
        input_path: Path to the input file
        output_path: Path to output file (if None and not in_place, prints to stdout)
        in_place: If True, modifies the input file directly
    """
    logger.info("Reading %s...", input_path)
    
    if not os.path.exists(input_path):
        logger.error("File not found: %s", input_path)
        return None

    try:
        with open(input_path, 'r', encoding='utf-8') as f:
            lines = [line.rstrip('\n\r') for line in f if line.strip()]
    except IOError as e:
        logger.error("Failed to read %s: %s", input_path, e)
        return None

    logger.info("Found %d lines", len(lines))

    # Reverse the list
    reversed_lines = list(reversed(lines))

    # Determine output destination
    if in_place:
        output_path = input_path

    if output_path:
        try:
            with open(output_path, 'w', encoding='utf-8') as f:
                for line in reversed_lines:
                    f.write(line + '\n')
        except IOError as e:
            logger.error("Failed to write %s: %s", output_path, e)
            return None
        
        logger.info("Reversed list saved to %s", output_path)
        logger.info("  First line: %s", reversed_lines[0] if reversed_lines else '(empty)')
        logger.info("  Last line:  %s", reversed_lines[-1] if reversed_lines else '(empty)')
    else:
        # Print to stdout
        for line in reversed_lines:
            print(line)

    return reversed_lines


def do_matching():
    """Run the fuzzy matching process."""
    # Validate input files exist
    if not os.path.exists('files1.txt'):
        logger.error("files1.txt not found")
        return
    
    if not os.path.exists('playlist-bulletized1.txt'):
        logger.error("playlist-bulletized1.txt not found")
        return
    
    # Read the files list
    logger.info("Reading files1.txt...")
    try:
        with open('files1.txt', 'r', encoding='utf-8') as f:
            lines = f.readlines()
    except IOError as e:
        logger.error("Failed to read files1.txt: %s", e)
        return

    mp3_files = []
    mp3_paths = []
    for line in lines:
        filename = extract_filename_from_ls_line(line)
        if filename:
            mp3_files.append(filename)
            mp3_paths.append(line.strip())

    logger.info("Found %d MP3 files", len(mp3_files))

    # Read the playlist
    logger.info("Reading playlist-bulletized1.txt...")
    try:
        with open('playlist-bulletized1.txt', 'r', encoding='utf-8') as f:
            lines = f.readlines()
    except IOError as e:
        logger.error("Failed to read playlist-bulletized1.txt: %s", e)
        return

    # Parse the playlist - skip first '[' and last ']' lines, and clean up entries
    playlist = []
    for line in lines[1:-1]:  # Skip first and last line
        # Remove leading/trailing whitespace and trailing comma
        entry = line.strip().rstrip(',')
        if entry and entry != "Private video":
            playlist.append(entry)

    logger.info("Found %d playlist entries (excluding private videos)", len(playlist))

    # Match playlist entries to MP3 files
    logger.info("")
    logger.info("Matching playlist entries to MP3 files...")
    logger.info("=" * 80)

    matches = []
    used_indices = set()

    for i, playlist_entry in enumerate(playlist, 1):
        best_match, score, match_idx = find_best_match(playlist_entry, mp3_files, used_indices)

        if best_match:
            used_indices.add(match_idx)
            matches.append({
                'order': i,
                'playlist_name': playlist_entry,
                'mp3_file': best_match,
                'match_score': score,
                'file_index': match_idx
            })

            logger.info("")
            logger.info("%d. %s", i, playlist_entry)
            logger.info("   → %s", best_match)
            logger.info("   (match score: %.2f%%)", score * 100)
        else:
            logger.info("")
            logger.info("%d. %s", i, playlist_entry)
            logger.info("   → NO MATCH FOUND")

    # Save results to JSON
    logger.info("")
    logger.info("=" * 80)
    logger.info("")
    logger.info("Saving results to matched_episodes.json...")
    try:
        with open('matched_episodes.json', 'w', encoding='utf-8') as f:
            json.dump(matches, f, indent=2, ensure_ascii=False)
    except IOError as e:
        logger.error("Failed to write matched_episodes.json: %s", e)
        return

    # Create a simple mapping file
    logger.info("Saving simple mapping to episode_mapping.txt...")
    try:
        with open('episode_mapping.txt', 'w', encoding='utf-8') as f:
            for match in matches:
                f.write(f"{match['order']}. {match['playlist_name']}\n")
                f.write(f"   → {match['mp3_file']}\n\n")
    except IOError as e:
        logger.error("Failed to write episode_mapping.txt: %s", e)
        return

    # Statistics
    logger.info("")
    logger.info("=" * 80)
    logger.info("")
    logger.info("Statistics:")
    logger.info("  Total playlist entries: %d", len(playlist))
    logger.info("  Total MP3 files: %d", len(mp3_files))
    logger.info("  Successful matches: %d", len(matches))
    logger.info("  Unmatched MP3 files: %d", len(mp3_files) - len(used_indices))

    avg_score = sum(m['match_score'] for m in matches) / len(matches) if matches else 0
    logger.info("  Average match score: %.2f%%", avg_score * 100)

    low_confidence = [m for m in matches if m['match_score'] < 0.7]
    if low_confidence:
        logger.warning("")
        logger.warning("  ⚠️  %d matches have low confidence (< 70%%):", len(low_confidence))
        for m in low_confidence[:5]:  # Show first 5
            logger.warning("     - %s → %s (%.2f%%)", m['playlist_name'], m['mp3_file'], m['match_score'] * 100)
        if len(low_confidence) > 5:
            logger.warning("     ... and %d more", len(low_confidence) - 5)

    logger.info("")
    logger.info("Done! Results saved to:")
    logger.info("  - matched_episodes.json (detailed JSON)")
    logger.info("  - episode_mapping.txt (human-readable)")


def main():
    parser = argparse.ArgumentParser(
        description='Match playlist episodes to MP3 files and rename them.',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  %(prog)s match                    # Run fuzzy matching
  %(prog)s rename --dry-run         # Preview renames (default)
  %(prog)s rename --execute         # Actually rename files
  %(prog)s script                   # Generate bash script for renames
  %(prog)s mapfile                  # Generate map file for podcast feed API
  %(prog)s reverse episode_order.txt --in-place  # Reverse file in place
  %(prog)s reverse input.txt -o output.txt       # Reverse to new file
        """
    )

    subparsers = parser.add_subparsers(dest='command', help='Available commands')

    # Match command
    match_parser = subparsers.add_parser('match', help='Run fuzzy matching to link playlist to MP3s')

    # Rename command
    rename_parser = subparsers.add_parser('rename', help='Rename MP3 files with order prefix')
    rename_parser.add_argument('--execute', action='store_true',
                               help='Actually rename files (default is dry-run)')
    rename_parser.add_argument('--dry-run', action='store_true', default=True,
                               help='Preview renames without making changes (default)')
    rename_parser.add_argument('--json', default='matched_episodes.json',
                               help='Path to matched episodes JSON file')
    rename_parser.add_argument('--files', default='files1.txt',
                               help='Path to files list')
    rename_parser.add_argument('--pad', type=int, default=3,
                               help='Zero-padding width for order number (default: 3)')

    # Script command
    script_parser = subparsers.add_parser('script', help='Generate a bash rename script')
    script_parser.add_argument('--json', default='matched_episodes.json',
                               help='Path to matched episodes JSON file')
    script_parser.add_argument('--files', default='files1.txt',
                               help='Path to files list')
    script_parser.add_argument('--output', default='rename_episodes.sh',
                               help='Output script path')
    script_parser.add_argument('--pad', type=int, default=3,
                               help='Zero-padding width for order number (default: 3)')

    # Map file command
    mapfile_parser = subparsers.add_parser('mapfile', help='Generate map file for podcast feed API')
    mapfile_parser.add_argument('--json', default='matched_episodes.json',
                                help='Path to matched episodes JSON file')
    mapfile_parser.add_argument('--output', default='episode_order.txt',
                                help='Output map file path')

    # Reverse command
    reverse_parser = subparsers.add_parser('reverse', help='Reverse the order of lines in a list file')
    reverse_parser.add_argument('input', help='Input file to reverse')
    reverse_parser.add_argument('-o', '--output', help='Output file (default: print to stdout)')
    reverse_parser.add_argument('--in-place', action='store_true',
                                help='Modify the input file in place')

    args = parser.parse_args()

    if args.command == 'match':
        do_matching()
    elif args.command == 'rename':
        dry_run = not args.execute
        rename_files(json_path=args.json, files1_path=args.files,
                     dry_run=dry_run, pad_width=args.pad)
    elif args.command == 'script':
        generate_rename_script(json_path=args.json, files1_path=args.files,
                               output_path=args.output, pad_width=args.pad)
    elif args.command == 'mapfile':
        generate_map_file(json_path=args.json, output_path=args.output)
    elif args.command == 'reverse':
        reverse_list_file(args.input, output_path=args.output, in_place=args.in_place)
    else:
        # Default to matching if no command specified
        parser.print_help()


if __name__ == '__main__':
    main()
