function read(source, ...keys) {
  for (const key of keys) {
    const value = source?.[key];
    if (value !== undefined && value !== null && value !== "") {
      return value;
    }
  }
  return undefined;
}

export function getJockeyDisplayStats(jockey) {
  const totalRaces = Number(read(jockey, "totalRaces", "TotalRaces") ?? 0);
  const totalWins = Number(read(jockey, "totalWins", "TotalWins") ?? 0);
  const winRateRaw = read(jockey, "winRate", "WinRate");
  const rankRaw = read(jockey, "leaderboardRank", "LeaderboardRank", "rank", "Rank");

  return {
    totalRaces,
    totalWins,
    winRate: winRateRaw != null ? Number(winRateRaw) : totalRaces > 0 ? Math.round((totalWins / totalRaces) * 100) : 0,
    rank: rankRaw != null ? Number(rankRaw) : null,
  };
}

