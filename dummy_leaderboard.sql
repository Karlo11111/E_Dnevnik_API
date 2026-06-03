-- Dummy leaderboard data for local dev
-- Run against your local Postgres: psql -U postgres -d ednevnik -f dummy_leaderboard.sql
-- Scores are pre-computed: CombinedScore = (StreakScore * 0.6) + (GradeDeltaScore * 30 * 0.4)
--                          StreakScore = totalSessions * (1 + CurrentStreak * 0.1)

-- Clear existing dummy data (safe to re-run)
DELETE FROM "LeaderboardEntries" WHERE "Email" LIKE 'dummy_%@test.com';

-- ============================================================
-- School: Tehnička škola Zagreb (ID: TSZ-01)
-- ============================================================

INSERT INTO "LeaderboardEntries"
  ("Email", "Nickname", "ClassId", "SchoolId", "City", "County", "StudentProgram",
   "GradeDeltaScore", "StreakScore", "CombinedScore", "CurrentStreak", "OptedInAt", "LastScoreUpdate")
VALUES
  -- 3.A class
  ('dummy_1@test.com', 'Brzina96',    '3.A', 'TSZ-01', 'Zagreb', 'Grad Zagreb', 'Tehničar za računalstvo',  1.20, 108.0, 79.20,  14, NOW(), NOW()),
  ('dummy_2@test.com', 'KodMonster',  '3.A', 'TSZ-01', 'Zagreb', 'Grad Zagreb', 'Tehničar za računalstvo',  0.80,  72.0, 52.80,   9, NOW(), NOW()),
  ('dummy_3@test.com', 'Matko_Z',     '3.A', 'TSZ-01', 'Zagreb', 'Grad Zagreb', 'Tehničar za računalstvo',  0.50,  51.0, 36.60,   7, NOW(), NOW()),
  ('dummy_4@test.com', 'Ana404',      '3.A', 'TSZ-01', 'Zagreb', 'Grad Zagreb', 'Tehničar za računalstvo', -0.20,  28.0, 14.40,   3, NOW(), NOW()),
  ('dummy_5@test.com', 'Tihi_Dev',    '3.A', 'TSZ-01', 'Zagreb', 'Grad Zagreb', 'Tehničar za računalstvo',  0.10,  12.0,  8.40,   1, NOW(), NOW()),

  -- 2.B class
  ('dummy_6@test.com',  'LoopBreaker', '2.B', 'TSZ-01', 'Zagreb', 'Grad Zagreb', 'Tehničar za računalstvo',  1.50, 135.0, 99.00,  21, NOW(), NOW()),
  ('dummy_7@test.com',  'Petra_T',     '2.B', 'TSZ-01', 'Zagreb', 'Grad Zagreb', 'Tehničar za računalstvo',  0.60,  60.0, 43.20,   8, NOW(), NOW()),
  ('dummy_8@test.com',  'NullPtr',     '2.B', 'TSZ-01', 'Zagreb', 'Grad Zagreb', 'Tehničar za računalstvo',  0.30,  34.0, 23.96,   4, NOW(), NOW()),
  ('dummy_9@test.com',  'Luka_07',     '2.B', 'TSZ-01', 'Zagreb', 'Grad Zagreb', 'Tehničar za računalstvo', -0.10,  10.0,  4.80,   0, NOW(), NOW()),
  ('dummy_10@test.com', 'Stackie',     '2.B', 'TSZ-01', 'Zagreb', 'Grad Zagreb', 'Tehničar za računalstvo',  0.40,  20.0, 16.80,   2, NOW(), NOW());

-- ============================================================
-- School: Ekonomska škola Split (ID: ESS-01)
-- ============================================================

INSERT INTO "LeaderboardEntries"
  ("Email", "Nickname", "ClassId", "SchoolId", "City", "County", "StudentProgram",
   "GradeDeltaScore", "StreakScore", "CombinedScore", "CurrentStreak", "OptedInAt", "LastScoreUpdate")
VALUES
  -- 4.A class
  ('dummy_11@test.com', 'Iva_Splitska', '4.A', 'ESS-01', 'Split', 'Splitsko-dalmatinska', 'Ekonomist',  1.30,  90.0, 69.60,  10, NOW(), NOW()),
  ('dummy_12@test.com', 'Marko_D',      '4.A', 'ESS-01', 'Split', 'Splitsko-dalmatinska', 'Ekonomist',  0.70,  56.0, 41.76,   6, NOW(), NOW()),
  ('dummy_13@test.com', 'Sunce_99',     '4.A', 'ESS-01', 'Split', 'Splitsko-dalmatinska', 'Ekonomist',  0.20,  25.0, 17.40,   3, NOW(), NOW()),
  ('dummy_14@test.com', 'Bruna_E',      '4.A', 'ESS-01', 'Split', 'Splitsko-dalmatinska', 'Ekonomist', -0.30,   8.0,  1.20,   1, NOW(), NOW()),

  -- 3.B class
  ('dummy_15@test.com', 'Cvita',        '3.B', 'ESS-01', 'Split', 'Splitsko-dalmatinska', 'Ekonomist',  0.90,  66.0, 50.40,  11, NOW(), NOW()),
  ('dummy_16@test.com', 'Tonko_S',      '3.B', 'ESS-01', 'Split', 'Splitsko-dalmatinska', 'Ekonomist',  0.40,  32.0, 24.00,   5, NOW(), NOW()),
  ('dummy_17@test.com', 'Klara_R',      '3.B', 'ESS-01', 'Split', 'Splitsko-dalmatinska', 'Ekonomist',  0.10,  11.0,  7.80,   0, NOW(), NOW());

-- ============================================================
-- Program-wide: Opća gimnazija (various schools)
-- ============================================================

INSERT INTO "LeaderboardEntries"
  ("Email", "Nickname", "ClassId", "SchoolId", "City", "County", "StudentProgram",
   "GradeDeltaScore", "StreakScore", "CombinedScore", "CurrentStreak", "OptedInAt", "LastScoreUpdate")
VALUES
  ('dummy_18@test.com', 'Ema_Rij',    '2.A', 'GIM-RIJ', 'Rijeka',  'Primorsko-goranska',   'Opća gimnazija',  1.10, 99.0, 72.60,  13, NOW(), NOW()),
  ('dummy_19@test.com', 'Filip_Os',   '1.A', 'GIM-OSI', 'Osijek',  'Osječko-baranjska',    'Opća gimnazija',  0.80, 64.0, 48.00,   8, NOW(), NOW()),
  ('dummy_20@test.com', 'Sara_Zd',    '3.A', 'GIM-ZAD', 'Zadar',   'Zadarska',             'Opća gimnazija',  0.50, 35.0, 27.00,   4, NOW(), NOW()),
  ('dummy_21@test.com', 'Juraj_V',    '4.A', 'GIM-ZAG', 'Zagreb',  'Grad Zagreb',          'Opća gimnazija',  1.40, 84.0, 67.20,  12, NOW(), NOW()),
  ('dummy_22@test.com', 'Monika_Z',   '2.B', 'GIM-ZAG', 'Zagreb',  'Grad Zagreb',          'Opća gimnazija',  0.30, 18.0, 14.40,   2, NOW(), NOW());
