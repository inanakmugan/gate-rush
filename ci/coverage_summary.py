"""Turn coverlet's Cobertura report into a per-assembly Markdown table.

Standard library only. Usage:
    python3 ci/coverage_summary.py <coverage.cobertura.xml> [more.xml ...]
Prints Markdown to stdout; the workflow appends it to the job summary.
Lines are counted per assembly by unique (file, line), so a line reported by
several classes (partial classes, nested types) is counted once.
"""
import sys
import xml.etree.ElementTree as ET


def collect(paths):
    lines = {}
    for path in paths:
        root = ET.parse(path).getroot()
        for package in root.iter("package"):
            name = package.get("name")
            per_assembly = lines.setdefault(name, {})
            for cls in package.iter("class"):
                filename = cls.get("filename")
                for line in cls.iter("line"):
                    key = (filename, int(line.get("number")))
                    hit = int(line.get("hits", "0")) > 0
                    per_assembly[key] = per_assembly.get(key, False) or hit
    return lines


def main(argv):
    if len(argv) < 2:
        print("usage: coverage_summary.py <cobertura.xml> [...]", file=sys.stderr)
        return 2
    lines = collect(argv[1:])
    rows = []
    total_covered = total_valid = 0
    for name in sorted(lines):
        valid = len(lines[name])
        covered = sum(1 for hit in lines[name].values() if hit)
        total_covered += covered
        total_valid += valid
        rows.append((name, covered, valid))
    rows.append(("**Total**", total_covered, total_valid))

    print("### Line coverage (engine-free assemblies)")
    print()
    print("| Assembly | Covered lines | Coverable lines | Line coverage |")
    print("|---|---:|---:|---:|")
    for name, covered, valid in rows:
        percent = 100.0 * covered / valid if valid else 0.0
        print(f"| {name} | {covered} | {valid} | {percent:.1f}% |")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
