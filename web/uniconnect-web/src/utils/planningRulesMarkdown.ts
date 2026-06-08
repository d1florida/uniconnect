const CLUSTER_ENABLED_LINE =
  '- Cluster stops by geography when multiple trucks run (reduces cross-region miles).';
const CLUSTER_DISABLED_LINE =
  '- Do not cluster stops by geography; balance routes by minutes or stops only.';

function isClusterMarkdownLine(line: string): boolean {
  const lower = line.toLowerCase();
  return (
    lower.includes('cluster') &&
    (lower.includes('geograph') ||
      lower.includes('region') ||
      lower.includes('territor') ||
      lower.includes('do not cluster') ||
      lower.includes("don't cluster"))
  );
}

function clusterStateFromMarkdown(markdown: string): boolean | null {
  const clusterLines = markdown.split('\n').filter(isClusterMarkdownLine);
  if (clusterLines.length === 0) return null;

  const lower = clusterLines.join('\n').toLowerCase();
  if (
    lower.includes('do not cluster') ||
    lower.includes("don't cluster") ||
    lower.includes('no cluster') ||
    lower.includes('disable cluster')
  ) {
    return false;
  }
  return true;
}

export function isGeographicClusteringEnabled(
  markdown: string,
  compiledPolicyJson?: string | null,
): boolean {
  const fromMarkdown = clusterStateFromMarkdown(markdown);
  if (fromMarkdown !== null) return fromMarkdown;

  if (compiledPolicyJson) {
    try {
      const policy = JSON.parse(compiledPolicyJson) as {
        fleet?: { useGeographicClustering?: boolean };
      };
      if (typeof policy.fleet?.useGeographicClustering === 'boolean') {
        return policy.fleet.useGeographicClustering;
      }
    } catch {
      /* fall through */
    }
  }

  return true;
}

export function setGeographicClusteringInMarkdown(markdown: string, enabled: boolean): string {
  const lines = markdown.split('\n');
  const withoutCluster = lines.filter((line) => !isClusterMarkdownLine(line));
  const clusterLine = enabled ? CLUSTER_ENABLED_LINE : CLUSTER_DISABLED_LINE;

  const fleetHeadingIndex = withoutCluster.findIndex((line) => {
    const trimmed = line.trim().toLowerCase();
    return trimmed === '## fleet' || trimmed === '# fleet';
  });

  if (fleetHeadingIndex >= 0) {
    let insertAt = fleetHeadingIndex + 1;
    while (insertAt < withoutCluster.length && withoutCluster[insertAt].trim() === '') {
      insertAt += 1;
    }
    while (
      insertAt < withoutCluster.length &&
      withoutCluster[insertAt].trim().startsWith('-') &&
      withoutCluster[insertAt].toLowerCase().includes('balance')
    ) {
      insertAt += 1;
    }
    withoutCluster.splice(insertAt, 0, clusterLine);
    return withoutCluster.join('\n');
  }

  const fleetSection = `## Fleet\n${clusterLine}\n`;
  if (withoutCluster.some((line) => line.trim().startsWith('#'))) {
    return `${withoutCluster.join('\n').trimEnd()}\n\n${fleetSection}`.trimStart();
  }

  return `${fleetSection}${withoutCluster.join('\n')}`.trimEnd();
}
