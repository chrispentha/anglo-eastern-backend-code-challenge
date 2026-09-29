#!/usr/bin/env python3
"""Merges the Cobertura reports of all test projects and fails when any production project
is below the line or branch threshold.

Usage: python3 scripts/coverage_gate.py <results-directory> [threshold-percent, default 90]
"""
import collections
import glob
import sys
import xml.etree.ElementTree as ET


def main() -> int:
    results_dir = sys.argv[1] if len(sys.argv) > 1 else "TestResults"
    threshold = float(sys.argv[2]) if len(sys.argv) > 2 else 90.0
    reports = glob.glob(f"{results_dir}/**/coverage.cobertura.xml", recursive=True)
    if not reports:
        print(f"No coverage reports found under {results_dir}")
        return 1

    # A line or branch counts as covered when any test project covered it.
    line_hits = collections.defaultdict(int)        # (project, file, line) -> hits
    branch_hits = {}                                 # (project, file, line) -> (covered, total)
    for report in reports:
        for package in ET.parse(report).getroot().iter("package"):
            project = package.get("name")
            for cls in package.iter("class"):
                file_name = cls.get("filename")
                for line in cls.find("lines").iter("line"):
                    key = (project, file_name, int(line.get("number")))
                    line_hits[key] = max(line_hits[key], int(line.get("hits")))
                    if line.get("branch") == "True":
                        covered, total = map(int, line.get("condition-coverage").split("(")[1].rstrip(")").split("/"))
                        previous = branch_hits.get(key, (0, total))
                        branch_hits[key] = (max(previous[0], covered), total)

    totals = collections.defaultdict(lambda: [0, 0, 0, 0])
    for (project, _, _), hits in line_hits.items():
        totals[project][0] += hits > 0
        totals[project][1] += 1
    for (project, _, _), (covered, total) in branch_hits.items():
        totals[project][2] += covered
        totals[project][3] += total

    failed = False
    print(f"{'Project':34} {'Line':>8} {'Branch':>8}")
    for project, (lc, lt, bc, bt) in sorted(totals.items()):
        line_pct = lc / lt * 100
        branch_pct = bc / bt * 100 if bt else 100.0
        ok = line_pct >= threshold and branch_pct >= threshold
        failed |= not ok
        print(f"{project:34} {line_pct:7.1f}% {branch_pct:7.1f}%  {'ok' if ok else f'BELOW {threshold:.0f}%'}")

    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
