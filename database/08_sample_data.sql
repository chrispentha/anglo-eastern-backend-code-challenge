/*
    08_sample_data.sql
    Realistic, fictional sample data (SEC-14: no real seafarers). Loaded once: skipped if SHIP01 exists.

    Contents
    - 5 ships: 3 active, 2 inactive (the inactive ones also have crew and finance data, to prove they are
      excluded), fiscal years 0112, 0403, 0706 and 1009.
    - 225 crew members; every ship has 22-23 started open contracts over 19 ranks, with a mix of
      Onboard, Onboard within 30 days after End of Contract, Relief Due, Planned and Signed Off,
      1-day handover overlaps, crew with earlier contracts on other ships (some in a lower rank).
    - Crew dates are relative to the day the seed runs (D-21), so the mix stays realistic whenever the
      demo is run. The brief's own records (CREW001, CREW002) keep their exact dates.
    - Chart of Accounts: root 7000000 with 7 parent accounts of 5-6 child accounts each (3 levels).
    - Budgets for SHIP01-SHIP04, every month of 2024-2026; actuals for all of 2024 and Jan 2025 - Aug 2026,
      1-3 transactions per period, including explicit zeros, duplicate budget lines, accounts with actuals
      but no budget and vice versa, and the brief's exact SHIP01 / 7135000 rows.
    - 6 users (one per role) with ship assignments. Their local development API keys are generated at random
      by 09_dev_api_keys.sql. Never load this script into a production database (SEED_SAMPLE_DATA=0).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF EXISTS (SELECT 1 FROM dbo.Ship WHERE ShipCode = 'SHIP01')
BEGIN
    PRINT 'Sample data already present - skipped.';
    RETURN;
END;

DECLARE @SeedDate DATE = CAST(SYSUTCDATETIME() AS DATE);

BEGIN TRANSACTION;

/* ============================== Ships ============================== */
INSERT INTO dbo.Ship (ShipCode, ShipName, FiscalYearCode, ShipStatusId)
VALUES
    ('SHIP01', N'Flying Dutchman',      '0112', 1),
    ('SHIP02', N'Thousand Sunny',       '0403', 1),
    ('SHIP03', N'Black Pearl',          '0706', 1),
    ('SHIP04', N'Going Merry',          '1009', 2),
    ('SHIP05', N'Queen Anne''s Revenge', '0112', 2);

/* ============================== Crew members ============================== */
INSERT INTO dbo.CrewMember (CrewMemberId, FirstName, LastName, BirthDate, NationalityCode)
VALUES
        ('CREW001', N'Soka', N'Philip', '1980-07-30', 'GR'),
        ('CREW002', N'Masteros', N'Philip', '1980-07-30', 'GR'),
        ('CREW003', N'John', N'Masterbear', '1975-03-12', 'GR'),
        ('CREW004', N'Rafael', N'Ortega', '1998-10-02', 'MX'),
        ('CREW005', N'John', N'Chena', '1995-05-20', 'MX'),
        ('CREW900', N'Leap', N'Dayson', '1988-02-29', 'GB'),
        ('CREW006', N'Jonas', N'Andersen', '1965-03-11', 'NO'),
        ('CREW007', N'Valdis', N'Ozoliņš', '1969-10-12', 'LV'),
        ('CREW008', N'Amit', N'Sharma', '1981-07-19', 'IN'),
        ('CREW009', N'Nikolaos', N'Karagiannis', '1988-09-03', 'GR'),
        ('CREW010', N'Panagiotis', N'Antoniou', '1985-11-26', 'GR'),
        ('CREW011', N'Christos', N'Makris', '1989-07-12', 'GR'),
        ('CREW012', N'Magnus', N'Berg', '1989-02-23', 'NO'),
        ('CREW013', N'Kristaps', N'Jansons', '1987-01-14', 'LV'),
        ('CREW014', N'Josip', N'Horvat', '1986-01-09', 'HR'),
        ('CREW015', N'George', N'Taylor', '1991-08-24', 'GB'),
        ('CREW016', N'Andrei', N'Munteanu', '1989-04-03', 'RO'),
        ('CREW017', N'Vikram', N'Menon', '1995-09-27', 'IN'),
        ('CREW018', N'Mykola', N'Shevchuk', '1995-11-05', 'UA'),
        ('CREW019', N'Hao', N'Liu', '1999-03-10', 'CN'),
        ('CREW020', N'Harry', N'Brown', '1999-02-16', 'GB'),
        ('CREW021', N'Piotr', N'Wójcik', '1998-05-14', 'PL'),
        ('CREW022', N'Suresh', N'Pillai', '2002-08-27', 'IN'),
        ('CREW023', N'George', N'Wilson', '2002-01-14', 'GB'),
        ('CREW024', N'Zaw', N'Win', '2003-11-15', 'MM'),
        ('CREW025', N'Myo', N'Tun', '1981-06-23', 'MM'),
        ('CREW026', N'Ramil', N'Cruz', '1980-03-22', 'PH'),
        ('CREW027', N'Roberto', N'García', '1981-02-10', 'MX'),
        ('CREW028', N'Minh', N'Nguyen', '1996-08-03', 'VN'),
        ('CREW029', N'Ramil', N'Garcia', '1991-01-05', 'PH'),
        ('CREW030', N'Budi', N'Pratama', '2003-04-17', 'ID'),
        ('CREW031', N'Rizki', N'Santoso', '2003-03-12', 'ID'),
        ('CREW032', N'Long', N'Le', '1989-02-04', 'VN'),
        ('CREW033', N'Thant', N'Maung', '1997-05-20', 'MM'),
        ('CREW034', N'Juan', N'Hernández', '1973-04-04', 'MX'),
        ('CREW035', N'Vikram', N'Iyer', '1993-09-11', 'IN'),
        ('CREW036', N'Tuan', N'Tran', '1983-06-05', 'VN'),
        ('CREW037', N'Ramil', N'Bautista', '2003-01-06', 'PH'),
        ('CREW038', N'Mark', N'Garcia', '2001-07-28', 'PH'),
        ('CREW039', N'John Paul', N'Bautista', '1972-07-14', 'PH'),
        ('CREW040', N'Dwi', N'Nugroho', '1971-12-23', 'ID'),
        ('CREW041', N'Adrian', N'Dumitru', '1993-12-10', 'RO'),
        ('CREW042', N'Jun', N'Chen', '1999-01-18', 'CN'),
        ('CREW043', N'Hao', N'Yang', '1991-04-01', 'CN'),
        ('CREW044', N'Yusuf', N'Hidayat', '1981-02-14', 'ID'),
        ('CREW045', N'Yong', N'Huang', '1986-05-01', 'CN'),
        ('CREW046', N'James', N'Brown', '1968-06-26', 'GB'),
        ('CREW047', N'Oliver', N'Hughes', '1980-02-11', 'GB'),
        ('CREW048', N'Mario', N'Marić', '1971-11-17', 'HR'),
        ('CREW049', N'Wei', N'Liu', '1974-08-05', 'CN'),
        ('CREW050', N'Thomas', N'Brown', '1991-11-15', 'GB'),
        ('CREW051', N'Jakub', N'Dąbrowski', '1976-01-12', 'PL'),
        ('CREW052', N'Suresh', N'Sharma', '1982-09-25', 'IN'),
        ('CREW053', N'Josip', N'Knežević', '1989-06-09', 'HR'),
        ('CREW054', N'Mihai', N'Stan', '1995-04-16', 'RO'),
        ('CREW055', N'Rahul', N'Rao', '1987-12-20', 'IN'),
        ('CREW056', N'James', N'Taylor', '1991-07-08', 'GB'),
        ('CREW057', N'Qiang', N'Wang', '1997-11-23', 'CN'),
        ('CREW058', N'Rohit', N'Iyer', '1987-11-05', 'IN'),
        ('CREW059', N'Mykola', N'Tkachenko', '1987-08-02', 'UA'),
        ('CREW060', N'Jānis', N'Bērziņš', '1999-07-22', 'LV'),
        ('CREW061', N'Harry', N'Wright', '1986-12-22', 'GB'),
        ('CREW062', N'Piotr', N'Kamiński', '1988-05-25', 'PL'),
        ('CREW063', N'Serhiy', N'Lysenko', '2002-01-17', 'UA'),
        ('CREW064', N'Karan', N'Nair', '2005-05-07', 'IN'),
        ('CREW065', N'Quang', N'Phan', '2005-09-09', 'VN'),
        ('CREW066', N'John Paul', N'Santos', '2006-11-20', 'PH'),
        ('CREW067', N'Kristaps', N'Liepiņš', '2002-11-09', 'LV'),
        ('CREW068', N'John Paul', N'Villanueva', '1980-08-05', 'PH'),
        ('CREW069', N'Yong', N'Wang', '1978-05-22', 'CN'),
        ('CREW070', N'Ștefan', N'Munteanu', '1974-12-22', 'RO'),
        ('CREW071', N'Jose', N'Dela Cruz', '1991-06-21', 'PH'),
        ('CREW072', N'Yong', N'Chen', '1991-12-02', 'CN'),
        ('CREW073', N'Roberto', N'González', '1986-06-09', 'MX'),
        ('CREW074', N'Fernando', N'Sánchez', '1988-04-27', 'MX'),
        ('CREW075', N'Mark', N'Villanueva', '1983-12-13', 'PH'),
        ('CREW076', N'Andi', N'Hidayat', '1992-07-25', 'ID'),
        ('CREW077', N'Mark', N'Santos', '1975-05-27', 'PH'),
        ('CREW078', N'Maria Clara', N'Mendoza', '1987-06-17', 'PH'),
        ('CREW079', N'Eko', N'Nugroho', '2000-05-11', 'ID'),
        ('CREW080', N'Rodel', N'Reyes', '1984-07-04', 'PH'),
        ('CREW081', N'Alexandru', N'Stan', '1973-11-12', 'RO'),
        ('CREW082', N'Hung', N'Vu', '1986-05-27', 'VN'),
        ('CREW083', N'Rodel', N'Garcia', '1984-10-05', 'PH'),
        ('CREW084', N'Rizki', N'Kurniawan', '1984-11-19', 'ID'),
        ('CREW085', N'Mihai', N'Rusu', '1991-06-19', 'RO'),
        ('CREW086', N'Lei', N'Zhang', '1990-05-13', 'CN'),
        ('CREW087', N'Karan', N'Pillai', '1995-02-27', 'IN'),
        ('CREW088', N'Tao', N'Chen', '1983-08-10', 'CN'),
        ('CREW089', N'Jorge', N'Sánchez', '2003-10-13', 'MX'),
        ('CREW090', N'Tomislav', N'Vuković', '1971-09-07', 'HR'),
        ('CREW091', N'Karan', N'Patel', '1977-05-13', 'IN'),
        ('CREW092', N'Luka', N'Knežević', '1977-12-27', 'HR'),
        ('CREW093', N'Tao', N'Li', '1966-10-22', 'CN'),
        ('CREW094', N'Josip', N'Marić', '1982-06-02', 'HR'),
        ('CREW095', N'Hao', N'Chen', '1980-07-15', 'CN'),
        ('CREW096', N'Raimonds', N'Balodis', '1977-12-17', 'LV'),
        ('CREW097', N'Sanjay', N'Iyer', '1983-03-13', 'IN'),
        ('CREW098', N'Emily', N'Walker', '1989-05-20', 'GB'),
        ('CREW099', N'Florin', N'Stan', '1990-06-17', 'RO'),
        ('CREW100', N'Rohit', N'Nair', '1992-09-28', 'IN'),
        ('CREW101', N'Kristaps', N'Zariņš', '1998-02-21', 'LV'),
        ('CREW102', N'George', N'Evans', '2000-06-11', 'GB'),
        ('CREW103', N'Florin', N'Rusu', '1992-05-24', 'RO'),
        ('CREW104', N'Mario', N'Jurić', '1987-02-25', 'HR'),
        ('CREW105', N'Nikolaos', N'Antoniou', '1987-06-21', 'GR'),
        ('CREW106', N'Yong', N'Yang', '1996-12-08', 'CN'),
        ('CREW107', N'Yuriy', N'Oliynyk', '1999-09-06', 'UA'),
        ('CREW108', N'Alexandru', N'Gheorghe', '2002-05-12', 'RO'),
        ('CREW109', N'Georgios', N'Vlachos', '2007-01-02', 'GR'),
        ('CREW110', N'Florin', N'Gheorghe', '2003-11-02', 'RO'),
        ('CREW111', N'Thomas', N'Wright', '2002-01-22', 'GB'),
        ('CREW112', N'Amit', N'Pillai', '2002-08-01', 'IN'),
        ('CREW113', N'Maria Clara', N'Garcia', '1979-08-26', 'PH'),
        ('CREW114', N'Bogdan', N'Stoica', '1974-04-25', 'RO'),
        ('CREW115', N'Roberto', N'Rodríguez', '1978-02-06', 'MX'),
        ('CREW116', N'Fernando', N'Hernández', '1972-06-21', 'MX'),
        ('CREW117', N'Kyaw', N'Lwin', '2001-10-01', 'MM'),
        ('CREW118', N'Aung', N'Tun', '1978-12-18', 'MM'),
        ('CREW119', N'Jose', N'Cruz', '1994-09-20', 'PH'),
        ('CREW120', N'Dwi', N'Wijaya', '1972-10-08', 'ID'),
        ('CREW121', N'Hendra', N'Setiawan', '1990-04-21', 'ID'),
        ('CREW122', N'Mark', N'Dela Cruz', '1995-05-10', 'PH'),
        ('CREW123', N'Andi', N'Wijaya', '1992-08-05', 'ID'),
        ('CREW124', N'Myo', N'Htun', '1994-09-05', 'MM'),
        ('CREW125', N'Hendra', N'Hidayat', '1994-04-21', 'ID'),
        ('CREW126', N'Aung', N'Thein', '2001-12-08', 'MM'),
        ('CREW127', N'Roberto', N'Pérez', '2001-01-04', 'MX'),
        ('CREW128', N'Rahul', N'Singh', '1993-08-17', 'IN'),
        ('CREW129', N'John Paul', N'Dela Cruz', '1982-07-14', 'PH'),
        ('CREW130', N'Jerome', N'Bautista', '1973-04-08', 'PH'),
        ('CREW131', N'Suresh', N'Iyer', '1980-11-21', 'IN'),
        ('CREW132', N'Cristian', N'Munteanu', '1992-11-28', 'RO'),
        ('CREW133', N'Maria Clara', N'Cruz', '2004-07-17', 'PH'),
        ('CREW134', N'Alexandru', N'Rusu', '1986-05-22', 'RO'),
        ('CREW135', N'Valdis', N'Bērziņš', '1967-03-17', 'LV'),
        ('CREW136', N'Ștefan', N'Stoica', '1980-08-06', 'RO'),
        ('CREW137', N'Mihai', N'Munteanu', '1970-03-22', 'RO'),
        ('CREW138', N'Christos', N'Stavrou', '1973-12-02', 'GR'),
        ('CREW139', N'Lei', N'Chen', '1978-09-06', 'CN'),
        ('CREW140', N'Wei', N'Li', '1978-11-16', 'CN'),
        ('CREW141', N'Eleni', N'Antoniou', '1985-09-16', 'GR'),
        ('CREW142', N'Luka', N'Vuković', '1991-08-03', 'HR'),
        ('CREW143', N'Josip', N'Babić', '1995-03-13', 'HR'),
        ('CREW144', N'Yong', N'Liu', '1994-12-23', 'CN'),
        ('CREW145', N'Christos', N'Nikolaidis', '1987-03-11', 'GR'),
        ('CREW146', N'Marko', N'Kovačević', '1987-11-04', 'HR'),
        ('CREW147', N'Ioannis', N'Georgiou', '2002-05-25', 'GR'),
        ('CREW148', N'Andrei', N'Stoica', '1993-08-20', 'RO'),
        ('CREW149', N'Suresh', N'Singh', '2000-03-04', 'IN'),
        ('CREW150', N'Bogdan', N'Munteanu', '1989-06-22', 'RO'),
        ('CREW151', N'Yong', N'Li', '1999-09-22', 'CN'),
        ('CREW152', N'Florin', N'Dumitru', '1999-11-13', 'RO'),
        ('CREW153', N'Davor', N'Babić', '2002-01-10', 'HR'),
        ('CREW154', N'Hao', N'Li', '2007-05-28', 'CN'),
        ('CREW155', N'Cristian', N'Stoica', '2002-03-10', 'RO'),
        ('CREW156', N'Amit', N'Nair', '2005-11-06', 'IN'),
        ('CREW157', N'Miguel', N'Sánchez', '2004-03-17', 'MX'),
        ('CREW158', N'Myo', N'Naing', '1980-03-08', 'MM'),
        ('CREW159', N'Rodel', N'Villanueva', '1992-07-01', 'PH'),
        ('CREW160', N'Andriy', N'Melnyk', '2002-11-09', 'UA'),
        ('CREW161', N'Tuan', N'Vu', '1997-11-12', 'VN'),
        ('CREW162', N'Tuan', N'Phan', '1983-05-21', 'VN'),
        ('CREW163', N'Bogdan', N'Gheorghe', '1990-11-09', 'RO'),
        ('CREW164', N'Mark', N'Cruz', '1998-02-28', 'PH'),
        ('CREW165', N'Mark', N'Mendoza', '2000-01-05', 'PH'),
        ('CREW166', N'Jerome', N'Cruz', '2003-06-17', 'PH'),
        ('CREW167', N'Fernando', N'González', '1987-02-07', 'MX'),
        ('CREW168', N'Duc', N'Pham', '1998-01-22', 'VN'),
        ('CREW169', N'Arjun', N'Patel', '1998-03-03', 'IN'),
        ('CREW170', N'Aung', N'Maung', '1973-11-27', 'MM'),
        ('CREW171', N'Thant', N'Tun', '1993-07-07', 'MM'),
        ('CREW172', N'Jose', N'Santos', '2003-02-21', 'PH'),
        ('CREW173', N'Serhiy', N'Melnyk', '2001-06-07', 'UA'),
        ('CREW174', N'Rodel', N'Cruz', '1979-08-19', 'PH'),
        ('CREW175', N'John Paul', N'Reyes', '1985-04-23', 'PH'),
        ('CREW176', N'Oleksandr', N'Kravchenko', '1995-04-26', 'UA'),
        ('CREW177', N'Jose', N'Villanueva', '1977-08-10', 'PH'),
        ('CREW178', N'Fernando', N'Pérez', '1990-11-27', 'MX'),
        ('CREW179', N'Jorge', N'Pérez', '1991-07-17', 'MX'),
        ('CREW180', N'Cristian', N'Gheorghe', '1978-07-15', 'RO'),
        ('CREW181', N'Serhiy', N'Tkachenko', '1978-05-23', 'UA'),
        ('CREW182', N'Andrei', N'Stan', '1972-12-22', 'RO'),
        ('CREW183', N'Erik', N'Hansen', '1973-03-28', 'NO'),
        ('CREW184', N'Eleni', N'Makris', '1978-04-12', 'GR'),
        ('CREW185', N'Emily', N'Smith', '1980-05-08', 'GB'),
        ('CREW186', N'Edgars', N'Zariņš', '1980-09-11', 'LV'),
        ('CREW187', N'Daniel', N'Smith', '1978-06-16', 'GB'),
        ('CREW188', N'Christos', N'Georgiou', '1986-07-10', 'GR'),
        ('CREW189', N'Alexandru', N'Ionescu', '1991-02-19', 'RO'),
        ('CREW190', N'Nikolaos', N'Vlachos', '1999-12-17', 'GR'),
        ('CREW191', N'Dmytro', N'Kravchenko', '1986-04-25', 'UA'),
        ('CREW192', N'Samuel', N'Brown', '1998-11-17', 'GB'),
        ('CREW193', N'Tomislav', N'Marić', '1991-02-04', 'HR'),
        ('CREW194', N'Jānis', N'Kalniņš', '1987-04-28', 'LV'),
        ('CREW195', N'Jonas', N'Berg', '1997-03-11', 'NO'),
        ('CREW196', N'Samuel', N'Wilson', '1986-06-13', 'GB'),
        ('CREW197', N'Dimitrios', N'Karagiannis', '1989-07-14', 'GR'),
        ('CREW198', N'Tomislav', N'Jurić', '1997-01-05', 'HR'),
        ('CREW199', N'Jorge', N'Rodríguez', '2004-09-09', 'MX'),
        ('CREW200', N'Hendra', N'Saputra', '2004-05-18', 'ID'),
        ('CREW201', N'Sanjay', N'Sharma', '2005-01-12', 'IN'),
        ('CREW202', N'Serhiy', N'Kravchenko', '2003-09-06', 'UA'),
        ('CREW203', N'Kyaw', N'Htun', '2001-08-28', 'MM'),
        ('CREW204', N'Hung', N'Tran', '1981-03-20', 'VN'),
        ('CREW205', N'Andi', N'Pratama', '1992-02-02', 'ID'),
        ('CREW206', N'Dwi', N'Pratama', '2004-08-15', 'ID'),
        ('CREW207', N'Minh', N'Pham', '1988-12-08', 'VN'),
        ('CREW208', N'Agus', N'Saputra', '1978-07-02', 'ID'),
        ('CREW209', N'Rodel', N'Santos', '1977-06-20', 'PH'),
        ('CREW210', N'Luis', N'Hernández', '1987-08-03', 'MX'),
        ('CREW211', N'Oleksandr', N'Tkachenko', '1992-11-01', 'UA'),
        ('CREW212', N'Ming', N'Li', '1977-12-24', 'CN'),
        ('CREW213', N'Rodel', N'Dela Cruz', '1984-07-14', 'PH'),
        ('CREW214', N'Qiang', N'Huang', '1986-04-25', 'CN'),
        ('CREW215', N'Duc', N'Le', '1986-08-25', 'VN'),
        ('CREW216', N'Thanh', N'Hoang', '1975-05-22', 'VN'),
        ('CREW217', N'Carlos', N'Pérez', '1980-06-22', 'MX'),
        ('CREW218', N'Roberto', N'Sánchez', '1974-01-15', 'MX'),
        ('CREW219', N'Ming', N'Wang', '1990-10-14', 'CN'),
        ('CREW220', N'Long', N'Pham', '1973-03-25', 'VN'),
        ('CREW221', N'John Paul', N'Cruz', '1993-12-26', 'PH'),
        ('CREW222', N'Hung', N'Hoang', '1990-05-25', 'VN'),
        ('CREW223', N'Jun', N'Liu', '1984-05-26', 'CN'),
        ('CREW224', N'Kyaw', N'Tun', '1993-05-07', 'MM');

/* ============================== Crew service history ============================== */
-- The brief's records and their predecessors: literal dates.
INSERT INTO dbo.CrewServiceHistory (CrewMemberId, ShipId, RankId, SignOnDate, EndOfContractDate, SignOffDate)
SELECT v.CrewMemberId, s.ShipId, r.RankId, v.SignOnDate, v.EndOfContractDate, v.SignOffDate
FROM (VALUES
        ('CREW001', 'SHIP01', 'MST', '2025-04-05', '2025-07-05', NULL),
        ('CREW002', 'SHIP01', 'CE',  '2025-04-04', '2025-07-04', NULL),
        ('CREW006', 'SHIP01', 'MST', '2024-10-01', '2025-04-01', '2025-04-06'),
        ('CREW007', 'SHIP01', 'CE', '2024-10-01', '2025-04-01', '2025-04-05')
     ) AS v (CrewMemberId, ShipCode, RankCode, SignOnDate, EndOfContractDate, SignOffDate)
INNER JOIN dbo.Ship AS s ON s.ShipCode = v.ShipCode
INNER JOIN dbo.CrewRank AS r ON r.RankCode = v.RankCode;

-- Everything else: day offsets relative to the seed date (negative = past, positive = future).
INSERT INTO dbo.CrewServiceHistory (CrewMemberId, ShipId, RankId, SignOnDate, EndOfContractDate, SignOffDate)
SELECT v.CrewMemberId, s.ShipId, r.RankId,
       DATEADD(DAY, v.SignOnOffset, @SeedDate),
       DATEADD(DAY, v.EocOffset, @SeedDate),
       DATEADD(DAY, v.SignOffOffset, @SeedDate)
FROM (VALUES
        ('CREW003', 'SHIP01', 'CO', -18, 170, NULL),
        ('CREW008', 'SHIP01', 'CO', -170, -24, -17),
        ('CREW003', 'SHIP02', '2O', -324, -121, -116),
        ('CREW009', 'SHIP01', '2E', -150, 20, NULL),
        ('CREW010', 'SHIP01', '2E', -369, -155, -149),
        ('CREW011', 'SHIP01', '2O', -181, -31, NULL),
        ('CREW012', 'SHIP01', '2O', -387, -186, -180),
        ('CREW013', 'SHIP01', '2O', -1, 149, NULL),
        ('CREW014', 'SHIP01', '3E', -154, 20, NULL),
        ('CREW015', 'SHIP01', '3E', -365, -161, -153),
        ('CREW016', 'SHIP01', '3O', -127, 50, NULL),
        ('CREW017', 'SHIP01', '3O', -343, -135, -126),
        ('CREW016', 'SHIP02', '3O', -386, -219, -214),
        ('CREW018', 'SHIP01', '4E', -170, -9, NULL),
        ('CREW019', 'SHIP01', '4E', -399, -180, -169),
        ('CREW020', 'SHIP01', 'ETO', -155, 50, NULL),
        ('CREW021', 'SHIP01', 'ETO', -377, -158, -154),
        ('CREW004', 'SHIP01', 'DCDT', -86, 170, NULL),
        ('CREW022', 'SHIP01', 'DCDT', -299, -86, -85),
        ('CREW023', 'SHIP01', 'ECDT', -99, 170, NULL),
        ('CREW024', 'SHIP01', 'ECDT', -321, -100, -98),
        ('CREW023', 'SHIP02', 'ECDT', -339, -193, -188),
        ('CREW025', 'SHIP01', 'BSN', -313, -82, NULL),
        ('CREW026', 'SHIP01', 'BSN', -540, -328, -312),
        ('CREW026', 'SHIP01', 'BSN', 28, 259, NULL),
        ('CREW027', 'SHIP01', 'FTR', -71, 110, NULL),
        ('CREW028', 'SHIP01', 'FTR', -236, -88, -70),
        ('CREW029', 'SHIP01', 'AB', -125, 80, NULL),
        ('CREW030', 'SHIP01', 'AB', -309, -127, -124),
        ('CREW031', 'SHIP01', 'AB', -21, 170, NULL),
        ('CREW032', 'SHIP01', 'AB', -179, -26, -20),
        ('CREW031', 'SHIP02', 'OS', -292, -127, -122),
        ('CREW033', 'SHIP01', 'AB', -117, 80, NULL),
        ('CREW034', 'SHIP01', 'AB', -300, -131, -116),
        ('CREW005', 'SHIP01', 'OLR', -231, -41, NULL),
        ('CREW035', 'SHIP01', 'OLR', -446, -236, -230),
        ('CREW036', 'SHIP01', 'OLR', -253, -52, NULL),
        ('CREW037', 'SHIP01', 'OLR', -469, -255, -252),
        ('CREW037', 'SHIP01', 'OLR', 28, 229, NULL),
        ('CREW038', 'SHIP01', 'CCK', -159, 50, NULL),
        ('CREW039', 'SHIP01', 'CCK', -327, -164, -158),
        ('CREW038', 'SHIP02', 'MSM', -456, -283, -278),
        ('CREW040', 'SHIP01', 'OS', -39, 230, NULL),
        ('CREW041', 'SHIP01', 'OS', -245, -56, -38),
        ('CREW042', 'SHIP01', 'MSM', -200, -9, NULL),
        ('CREW043', 'SHIP01', 'MSM', -382, -212, -199),
        ('CREW044', 'SHIP01', 'WPR', -107, 110, NULL),
        ('CREW045', 'SHIP01', 'WPR', -341, -120, -106),
        ('CREW046', 'SHIP02', 'MST', -95, 80, NULL),
        ('CREW047', 'SHIP02', 'MST', -309, -106, -94),
        ('CREW048', 'SHIP02', 'CE', -181, -27, NULL),
        ('CREW049', 'SHIP02', 'CE', -360, -180, -180),
        ('CREW050', 'SHIP02', 'CO', -55, 110, NULL),
        ('CREW051', 'SHIP02', 'CO', -232, -55, -54),
        ('CREW050', 'SHIP03', '2O', -302, -135, -130),
        ('CREW052', 'SHIP02', '2E', -102, 80, NULL),
        ('CREW053', 'SHIP02', '2E', -264, -105, -101),
        ('CREW054', 'SHIP02', '2O', -77, 110, NULL),
        ('CREW055', 'SHIP02', '2O', -286, -83, -76),
        ('CREW056', 'SHIP02', '3E', -321, -116, NULL),
        ('CREW057', 'SHIP02', '3E', -477, -322, -320),
        ('CREW058', 'SHIP02', '3E', -1, 204, NULL),
        ('CREW900', 'SHIP02', '3O', -126, 50, NULL),
        ('CREW059', 'SHIP02', '3O', -299, -131, -125),
        ('CREW900', 'SHIP03', '3O', -367, -217, -212),
        ('CREW060', 'SHIP02', '4E', -38, 170, NULL),
        ('CREW061', 'SHIP02', '4E', -244, -41, -37),
        ('CREW062', 'SHIP02', 'ETO', -209, -26, NULL),
        ('CREW063', 'SHIP02', 'ETO', -369, -219, -208),
        ('CREW064', 'SHIP02', 'DCDT', -42, 140, NULL),
        ('CREW065', 'SHIP02', 'DCDT', -248, -52, -41),
        ('CREW066', 'SHIP02', 'ECDT', -149, 110, NULL),
        ('CREW067', 'SHIP02', 'ECDT', -321, -156, -148),
        ('CREW066', 'SHIP03', 'ECDT', -421, -263, -258),
        ('CREW068', 'SHIP02', 'BSN', -48, 170, NULL),
        ('CREW069', 'SHIP02', 'BSN', -216, -60, -47),
        ('CREW070', 'SHIP02', 'FTR', -20, 230, NULL),
        ('CREW071', 'SHIP02', 'FTR', -232, -21, -19),
        ('CREW072', 'SHIP02', 'AB', -309, -80, NULL),
        ('CREW073', 'SHIP02', 'AB', -519, -323, -308),
        ('CREW073', 'SHIP02', 'AB', 26, 255, NULL),
        ('CREW074', 'SHIP02', 'AB', -90, 140, NULL),
        ('CREW075', 'SHIP02', 'AB', -284, -100, -89),
        ('CREW074', 'SHIP03', 'OS', -352, -205, -200),
        ('CREW076', 'SHIP02', 'AB', -22, 230, NULL),
        ('CREW077', 'SHIP02', 'AB', -251, -35, -21),
        ('CREW078', 'SHIP02', 'OLR', -81, 140, NULL),
        ('CREW079', 'SHIP02', 'OLR', -241, -99, -80),
        ('CREW080', 'SHIP02', 'OLR', -88, 140, NULL),
        ('CREW081', 'SHIP02', 'OLR', -306, -96, -87),
        ('CREW082', 'SHIP02', 'CCK', -303, -77, NULL),
        ('CREW083', 'SHIP02', 'CCK', -464, -319, -302),
        ('CREW083', 'SHIP02', 'CCK', 23, 249, NULL),
        ('CREW082', 'SHIP03', 'MSM', -604, -414, -409),
        ('CREW084', 'SHIP02', 'OS', -104, 140, NULL),
        ('CREW085', 'SHIP02', 'OS', -275, -119, -103),
        ('CREW086', 'SHIP02', 'MSM', -34, 230, NULL),
        ('CREW087', 'SHIP02', 'MSM', -239, -35, -33),
        ('CREW088', 'SHIP02', 'WPR', -33, 200, NULL),
        ('CREW089', 'SHIP02', 'WPR', -227, -49, -32),
        ('CREW090', 'SHIP03', 'MST', -14, 170, NULL),
        ('CREW091', 'SHIP03', 'MST', -185, -20, -13),
        ('CREW092', 'SHIP03', 'CE', -74, 110, NULL),
        ('CREW093', 'SHIP03', 'CE', -264, -84, -73),
        ('CREW094', 'SHIP03', 'CO', -34, 140, NULL),
        ('CREW095', 'SHIP03', 'CO', -220, -44, -33),
        ('CREW094', 'SHIP04', '2O', -290, -123, -118),
        ('CREW096', 'SHIP03', '2E', -118, 50, NULL),
        ('CREW097', 'SHIP03', '2E', -309, -135, -117),
        ('CREW098', 'SHIP03', '2O', -218, -23, NULL),
        ('CREW099', 'SHIP03', '2O', -368, -217, -217),
        ('CREW100', 'SHIP03', '3E', -32, 140, NULL),
        ('CREW101', 'SHIP03', '3E', -202, -48, -31),
        ('CREW102', 'SHIP03', '3O', -221, -66, NULL),
        ('CREW103', 'SHIP03', '3O', -446, -225, -220),
        ('CREW104', 'SHIP03', '3O', -1, 154, NULL),
        ('CREW102', 'SHIP04', '3O', -475, -291, -286),
        ('CREW105', 'SHIP03', '4E', -110, 80, NULL),
        ('CREW106', 'SHIP03', '4E', -268, -110, -109),
        ('CREW107', 'SHIP03', 'ETO', -131, 20, NULL),
        ('CREW108', 'SHIP03', 'ETO', -351, -130, -130),
        ('CREW109', 'SHIP03', 'DCDT', -235, -19, NULL),
        ('CREW110', 'SHIP03', 'DCDT', -436, -247, -234),
        ('CREW111', 'SHIP03', 'ECDT', -39, 230, NULL),
        ('CREW112', 'SHIP03', 'ECDT', -237, -43, -38),
        ('CREW111', 'SHIP04', 'ECDT', -290, -133, -128),
        ('CREW113', 'SHIP03', 'BSN', -149, 80, NULL),
        ('CREW114', 'SHIP03', 'BSN', -318, -164, -148),
        ('CREW115', 'SHIP03', 'FTR', -26, 170, NULL),
        ('CREW116', 'SHIP03', 'FTR', -176, -28, -25),
        ('CREW117', 'SHIP03', 'AB', -131, 80, NULL),
        ('CREW118', 'SHIP03', 'AB', -348, -135, -130),
        ('CREW119', 'SHIP03', 'AB', -84, 170, NULL),
        ('CREW120', 'SHIP03', 'AB', -297, -89, -83),
        ('CREW119', 'SHIP04', 'OS', -340, -193, -188),
        ('CREW121', 'SHIP03', 'AB', -315, -83, NULL),
        ('CREW122', 'SHIP03', 'AB', -534, -314, -314),
        ('CREW122', 'SHIP03', 'AB', 29, 261, NULL),
        ('CREW123', 'SHIP03', 'OLR', -28, 200, NULL),
        ('CREW124', 'SHIP03', 'OLR', -263, -42, -27),
        ('CREW125', 'SHIP03', 'OLR', -62, 170, NULL),
        ('CREW126', 'SHIP03', 'OLR', -281, -67, -61),
        ('CREW127', 'SHIP03', 'CCK', -155, 50, NULL),
        ('CREW128', 'SHIP03', 'CCK', -314, -163, -154),
        ('CREW127', 'SHIP04', 'MSM', -416, -245, -240),
        ('CREW129', 'SHIP03', 'OS', -375, -113, NULL),
        ('CREW130', 'SHIP03', 'OS', -547, -392, -374),
        ('CREW130', 'SHIP03', 'OS', 29, 291, NULL),
        ('CREW131', 'SHIP03', 'MSM', -134, 80, NULL),
        ('CREW132', 'SHIP03', 'MSM', -286, -133, -133),
        ('CREW133', 'SHIP03', 'WPR', -24, 230, NULL),
        ('CREW134', 'SHIP03', 'WPR', -228, -40, -23),
        ('CREW135', 'SHIP04', 'MST', -108, 80, NULL),
        ('CREW136', 'SHIP04', 'MST', -345, -126, -107),
        ('CREW137', 'SHIP04', 'CE', -27, 140, NULL),
        ('CREW138', 'SHIP04', 'CE', -245, -28, -26),
        ('CREW139', 'SHIP04', 'CO', -49, 110, NULL),
        ('CREW140', 'SHIP04', 'CO', -283, -59, -48),
        ('CREW139', 'SHIP05', '2O', -303, -123, -118),
        ('CREW141', 'SHIP04', '2E', -40, 110, NULL),
        ('CREW142', 'SHIP04', '2E', -197, -40, -39),
        ('CREW143', 'SHIP04', '2O', -154, 50, NULL),
        ('CREW144', 'SHIP04', '2O', -346, -167, -153),
        ('CREW145', 'SHIP04', '3E', -120, 50, NULL),
        ('CREW146', 'SHIP04', '3E', -281, -125, -119),
        ('CREW147', 'SHIP04', '3O', -70, 110, NULL),
        ('CREW148', 'SHIP04', '3O', -273, -77, -69),
        ('CREW147', 'SHIP05', '3O', -340, -165, -160),
        ('CREW149', 'SHIP04', '4E', -275, -93, NULL),
        ('CREW150', 'SHIP04', '4E', -437, -279, -274),
        ('CREW151', 'SHIP04', '4E', -1, 181, NULL),
        ('CREW152', 'SHIP04', 'ETO', -37, 140, NULL),
        ('CREW153', 'SHIP04', 'ETO', -273, -50, -36),
        ('CREW154', 'SHIP04', 'DCDT', -100, 140, NULL),
        ('CREW155', 'SHIP04', 'DCDT', -303, -110, -99),
        ('CREW156', 'SHIP04', 'ECDT', -223, -23, NULL),
        ('CREW157', 'SHIP04', 'ECDT', -422, -238, -222),
        ('CREW156', 'SHIP05', 'ECDT', -507, -338, -333),
        ('CREW158', 'SHIP04', 'BSN', -152, 110, NULL),
        ('CREW159', 'SHIP04', 'BSN', -390, -153, -151),
        ('CREW160', 'SHIP04', 'FTR', -152, 50, NULL),
        ('CREW161', 'SHIP04', 'FTR', -327, -155, -151),
        ('CREW162', 'SHIP04', 'AB', -77, 170, NULL),
        ('CREW163', 'SHIP04', 'AB', -289, -82, -76),
        ('CREW164', 'SHIP04', 'AB', -134, 50, NULL),
        ('CREW165', 'SHIP04', 'AB', -357, -152, -133),
        ('CREW164', 'SHIP05', 'OS', -422, -233, -228),
        ('CREW166', 'SHIP04', 'AB', -77, 170, NULL),
        ('CREW167', 'SHIP04', 'AB', -264, -85, -76),
        ('CREW168', 'SHIP04', 'OLR', -34, 170, NULL),
        ('CREW169', 'SHIP04', 'OLR', -193, -49, -33),
        ('CREW170', 'SHIP04', 'OLR', -229, -40, NULL),
        ('CREW171', 'SHIP04', 'OLR', -444, -234, -228),
        ('CREW171', 'SHIP04', 'OLR', 16, 205, NULL),
        ('CREW172', 'SHIP04', 'CCK', -57, 200, NULL),
        ('CREW173', 'SHIP04', 'CCK', -213, -59, -56),
        ('CREW172', 'SHIP05', 'MSM', -352, -169, -164),
        ('CREW174', 'SHIP04', 'OS', -73, 140, NULL),
        ('CREW175', 'SHIP04', 'OS', -265, -83, -72),
        ('CREW176', 'SHIP04', 'MSM', -385, -118, NULL),
        ('CREW177', 'SHIP04', 'MSM', -615, -395, -384),
        ('CREW177', 'SHIP04', 'MSM', 34, 301, NULL),
        ('CREW178', 'SHIP04', 'WPR', -122, 140, NULL),
        ('CREW179', 'SHIP04', 'WPR', -278, -134, -121),
        ('CREW180', 'SHIP05', 'MST', -67, 140, NULL),
        ('CREW181', 'SHIP05', 'MST', -294, -82, -66),
        ('CREW182', 'SHIP05', 'CE', -37, 170, NULL),
        ('CREW183', 'SHIP05', 'CE', -237, -54, -36),
        ('CREW184', 'SHIP05', 'CO', -10, 140, NULL),
        ('CREW185', 'SHIP05', 'CO', -213, -24, -9),
        ('CREW184', 'SHIP01', '2O', -258, -75, -70),
        ('CREW186', 'SHIP05', '2E', -18, 170, NULL),
        ('CREW187', 'SHIP05', '2E', -197, -21, -17),
        ('CREW188', 'SHIP05', '2O', -60, 110, NULL),
        ('CREW189', 'SHIP05', '2O', -298, -65, -59),
        ('CREW190', 'SHIP05', '3E', -10, 170, NULL),
        ('CREW191', 'SHIP05', '3E', -246, -9, -9),
        ('CREW192', 'SHIP05', '3O', -85, 80, NULL),
        ('CREW193', 'SHIP05', '3O', -257, -88, -84),
        ('CREW192', 'SHIP01', '3O', -353, -165, -160),
        ('CREW194', 'SHIP05', '4E', -38, 170, NULL),
        ('CREW195', 'SHIP05', '4E', -241, -38, -37),
        ('CREW196', 'SHIP05', 'ETO', -221, -66, NULL),
        ('CREW197', 'SHIP05', 'ETO', -422, -221, -220),
        ('CREW198', 'SHIP05', 'ETO', -1, 154, NULL),
        ('CREW199', 'SHIP05', 'DCDT', -43, 200, NULL),
        ('CREW200', 'SHIP05', 'DCDT', -263, -56, -42),
        ('CREW201', 'SHIP05', 'ECDT', -214, -26, NULL),
        ('CREW202', 'SHIP05', 'ECDT', -437, -222, -213),
        ('CREW201', 'SHIP01', 'ECDT', -496, -317, -312),
        ('CREW203', 'SHIP05', 'BSN', -225, -4, NULL),
        ('CREW204', 'SHIP05', 'BSN', -402, -225, -224),
        ('CREW205', 'SHIP05', 'FTR', -54, 170, NULL),
        ('CREW206', 'SHIP05', 'FTR', -215, -53, -53),
        ('CREW207', 'SHIP05', 'AB', -114, 80, NULL),
        ('CREW208', 'SHIP05', 'AB', -313, -125, -113),
        ('CREW209', 'SHIP05', 'AB', -127, 80, NULL),
        ('CREW210', 'SHIP05', 'AB', -343, -135, -126),
        ('CREW209', 'SHIP01', 'OS', -365, -219, -214),
        ('CREW211', 'SHIP05', 'AB', -20, 230, NULL),
        ('CREW212', 'SHIP05', 'AB', -183, -31, -19),
        ('CREW213', 'SHIP05', 'OLR', -16, 230, NULL),
        ('CREW214', 'SHIP05', 'OLR', -252, -18, -15),
        ('CREW215', 'SHIP05', 'OLR', -104, 80, NULL),
        ('CREW216', 'SHIP05', 'OLR', -253, -120, -103),
        ('CREW217', 'SHIP05', 'CCK', -120, 80, NULL),
        ('CREW218', 'SHIP05', 'CCK', -344, -124, -119),
        ('CREW217', 'SHIP01', 'MSM', -439, -235, -230),
        ('CREW219', 'SHIP05', 'OS', -215, -33, NULL),
        ('CREW220', 'SHIP05', 'OS', -388, -233, -214),
        ('CREW220', 'SHIP05', 'OS', 9, 191, NULL),
        ('CREW221', 'SHIP05', 'MSM', -17, 170, NULL),
        ('CREW222', 'SHIP05', 'MSM', -223, -30, -16),
        ('CREW223', 'SHIP05', 'WPR', -383, -117, NULL),
        ('CREW224', 'SHIP05', 'WPR', -580, -384, -382),
        ('CREW224', 'SHIP05', 'WPR', 33, 299, NULL)
     ) AS v (CrewMemberId, ShipCode, RankCode, SignOnOffset, EocOffset, SignOffOffset)
INNER JOIN dbo.Ship AS s ON s.ShipCode = v.ShipCode
INNER JOIN dbo.CrewRank AS r ON r.RankCode = v.RankCode;

/* ============================== Chart of Accounts ============================== */
DECLARE @Coa TABLE (AccountNumber VARCHAR(20) PRIMARY KEY, Description NVARCHAR(200), AccountType CHAR(1),
                    ParentNumber VARCHAR(20) NULL, Lvl TINYINT);
INSERT INTO @Coa (AccountNumber, Description, AccountType, ParentNumber, Lvl)
VALUES
    ('7000000', N'OPERATING EXPENSES',                  'P', NULL,      1),
    ('7100000', N'AWARD AND GRANT TO INDIVIDUALS',      'P', '7000000', 2),
    ('7110000', N'PERFORMANCE BONUSES',                 'C', '7100000', 3),
    ('7120000', N'AWARDS',                              'C', '7100000', 3),
    ('7135000', N'SCHOLARSHIPS',                        'C', '7100000', 3),
    ('7140000', N'CADET TRAINING GRANTS',               'C', '7100000', 3),
    ('7150000', N'WELFARE GRANTS',                      'C', '7100000', 3),
    ('7200000', N'CREW COSTS',                          'P', '7000000', 2),
    ('7210000', N'CREW WAGES',                          'C', '7200000', 3),
    ('7220000', N'OVERTIME',                            'C', '7200000', 3),
    ('7230000', N'VICTUALLING',                         'C', '7200000', 3),
    ('7240000', N'CREW TRAVEL',                         'C', '7200000', 3),
    ('7250000', N'CREW MEDICAL',                        'C', '7200000', 3),
    ('7260000', N'MANNING AGENCY FEES',                 'C', '7200000', 3),
    ('7300000', N'STORES AND SPARES',                   'P', '7000000', 2),
    ('7310000', N'DECK STORES',                         'C', '7300000', 3),
    ('7320000', N'ENGINE STORES',                       'C', '7300000', 3),
    ('7330000', N'CABIN STORES',                        'C', '7300000', 3),
    ('7340000', N'CHEMICALS AND GASES',                 'C', '7300000', 3),
    ('7350000', N'SPARE PARTS',                         'C', '7300000', 3),
    ('7400000', N'LUBRICATING OILS',                    'P', '7000000', 2),
    ('7410000', N'MAIN ENGINE SYSTEM OIL',              'C', '7400000', 3),
    ('7420000', N'AUXILIARY ENGINE OIL',                'C', '7400000', 3),
    ('7430000', N'CYLINDER OIL',                        'C', '7400000', 3),
    ('7440000', N'HYDRAULIC OIL',                       'C', '7400000', 3),
    ('7450000', N'GREASES',                             'C', '7400000', 3),
    ('7500000', N'REPAIRS AND MAINTENANCE',             'P', '7000000', 2),
    ('7510000', N'HULL MAINTENANCE',                    'C', '7500000', 3),
    ('7520000', N'MACHINERY REPAIRS',                   'C', '7500000', 3),
    ('7530000', N'ELECTRICAL REPAIRS',                  'C', '7500000', 3),
    ('7540000', N'SURVEYS AND CLASS FEES',              'C', '7500000', 3),
    ('7550000', N'SERVICE ENGINEERS',                   'C', '7500000', 3),
    ('7600000', N'INSURANCE',                           'P', '7000000', 2),
    ('7610000', N'HULL AND MACHINERY',                  'C', '7600000', 3),
    ('7620000', N'PROTECTION AND INDEMNITY',            'C', '7600000', 3),
    ('7630000', N'WAR RISK',                            'C', '7600000', 3),
    ('7640000', N'LOSS OF HIRE',                        'C', '7600000', 3),
    ('7650000', N'INSURANCE DEDUCTIBLES',               'C', '7600000', 3),
    ('7700000', N'MANAGEMENT AND ADMINISTRATION',       'P', '7000000', 2),
    ('7710000', N'MANAGEMENT FEE',                      'C', '7700000', 3),
    ('7720000', N'COMMUNICATIONS',                      'C', '7700000', 3),
    ('7730000', N'FLAG AND REGISTRATION FEES',          'C', '7700000', 3),
    ('7740000', N'BANK CHARGES',                        'C', '7700000', 3),
    ('7750000', N'SUNDRIES',                            'C', '7700000', 3);

-- Insert level by level so every parent exists before its children.
DECLARE @Lvl TINYINT = 1;
WHILE @Lvl <= (SELECT MAX(Lvl) FROM @Coa)
BEGIN
    INSERT INTO dbo.Account (AccountNumber, Description, AccountType, ParentAccountId)
    SELECT c.AccountNumber, c.Description, c.AccountType, p.AccountId
    FROM @Coa AS c
    LEFT JOIN dbo.Account AS p ON p.AccountNumber = c.ParentNumber
    WHERE c.Lvl = @Lvl;
    SET @Lvl += 1;
END;

/* ============================== Budgets and actuals ============================== */
/*
    Monthly figures per account (USD, one ship, 2024 level).
    BudgetPattern: M = every month, D = every month as two budget lines (summed, D-05), S = non-zero in
                   Jun and Dec only (explicit zeros otherwise), Z = explicit zero every month, N = no budget
    ActualPattern: M = every month, S = Jun and Dec only, O = occasional, N = no actuals
*/
DECLARE @Plan TABLE (AccountNumber VARCHAR(20) PRIMARY KEY, BudgetBase DECIMAL(19, 2), BudgetPattern CHAR(1),
                     ActualBase DECIMAL(19, 2), ActualPattern CHAR(1));
INSERT INTO @Plan (AccountNumber, BudgetBase, BudgetPattern, ActualBase, ActualPattern)
VALUES
    ('7110000',  1500, 'M',  1500, 'M'),
    ('7120000',  3000, 'S',  3000, 'S'),
    ('7135000',  1000, 'M',  1000, 'M'),
    ('7140000',     0, 'N',   600, 'O'),
    ('7150000',   700, 'M',     0, 'N'),
    ('7210000', 95000, 'D', 95000, 'M'),
    ('7220000', 12000, 'M', 12000, 'M'),
    ('7230000',  9000, 'M',  9000, 'M'),
    ('7240000',  8000, 'M',  8000, 'M'),
    ('7250000',  2500, 'M',  2500, 'M'),
    ('7260000',  4000, 'M',  4000, 'M'),
    ('7310000',  6000, 'M',  6000, 'M'),
    ('7320000',  7500, 'M',  7500, 'M'),
    ('7330000',  2500, 'M',  2500, 'M'),
    ('7340000',  3000, 'M',  3000, 'M'),
    ('7350000', 18000, 'M', 18000, 'M'),
    ('7410000',  9000, 'M',  9000, 'M'),
    ('7420000',  4000, 'M',  4000, 'M'),
    ('7430000', 11000, 'M', 11000, 'M'),
    ('7440000',  1200, 'M',  1200, 'M'),
    ('7450000',   600, 'M',   600, 'M'),
    ('7510000',  5000, 'M',  5000, 'M'),
    ('7520000', 14000, 'M', 14000, 'M'),
    ('7530000',  4000, 'M',  4000, 'M'),
    ('7540000',  3500, 'M',  3500, 'M'),
    ('7550000',  4500, 'M',  4500, 'M'),
    ('7610000', 12000, 'M', 12000, 'M'),
    ('7620000', 10000, 'M', 10000, 'M'),
    ('7630000',  1500, 'M',  1500, 'M'),
    ('7640000',  2000, 'M',  2000, 'M'),
    ('7650000',     0, 'Z', 12000, 'O'),
    ('7710000', 13000, 'M', 13000, 'M'),
    ('7720000',  2200, 'M',  2200, 'M'),
    ('7730000',   900, 'M',   900, 'M'),
    ('7740000',   300, 'M',   300, 'M'),
    ('7750000',  1200, 'M',  1200, 'M');

DECLARE @ShipPlan TABLE (ShipCode VARCHAR(10) PRIMARY KEY, Factor DECIMAL(5, 2));
INSERT INTO @ShipPlan (ShipCode, Factor)
VALUES ('SHIP01', 1.00), ('SHIP02', 1.15), ('SHIP03', 0.90), ('SHIP04', 0.80);

DECLARE @Month TABLE (AccountPeriod DATE PRIMARY KEY);
INSERT INTO @Month (AccountPeriod)
SELECT DATEADD(MONTH, n.i, '2024-01-01')
FROM (SELECT TOP (36) i = ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 FROM sys.all_objects) AS n;

-- Budgets: 2024-01 .. 2026-12. Flat within a year per ship/account, +3% a year.
INSERT INTO dbo.BudgetEntry (ShipId, AccountId, AccountPeriod, BudgetAmount)
SELECT s.ShipId, a.AccountId, m.AccountPeriod,
       CASE
           WHEN p.BudgetPattern = 'Z' THEN 0
           WHEN p.BudgetPattern = 'S' AND MONTH(m.AccountPeriod) NOT IN (6, 12) THEN 0
           ELSE ROUND(p.BudgetBase * sp.Factor * (1 + (YEAR(m.AccountPeriod) - 2024) * 0.03)
                      * (0.96 + ((CHECKSUM(s.ShipCode, a.AccountNumber, YEAR(m.AccountPeriod)) & 0x7FFFFFFF) % 9) / 100.0)
                      * CASE WHEN p.BudgetPattern = 'D' THEN CASE l.LineNum WHEN 1 THEN 0.70 ELSE 0.30 END ELSE 1 END, -1)
       END
FROM @ShipPlan AS sp
INNER JOIN dbo.Ship AS s ON s.ShipCode = sp.ShipCode
CROSS JOIN @Plan AS p
INNER JOIN dbo.Account AS a ON a.AccountNumber = p.AccountNumber
CROSS JOIN @Month AS m
CROSS JOIN (VALUES (1), (2)) AS l (LineNum)
WHERE p.BudgetPattern <> 'N'
  AND (l.LineNum = 1 OR p.BudgetPattern = 'D')
  -- the brief's exact SHIP01 / 7135000 rows are inserted below instead
  AND NOT (s.ShipCode = 'SHIP01' AND a.AccountNumber = '7135000' AND m.AccountPeriod IN ('2025-01-01', '2025-02-01', '2025-12-01'));

INSERT INTO dbo.BudgetEntry (ShipId, AccountId, AccountPeriod, BudgetAmount)
SELECT s.ShipId, a.AccountId, v.AccountPeriod, v.BudgetAmount
FROM (VALUES ('2025-01-01', 0), ('2025-01-01', 1000), ('2025-02-01', 1200), ('2025-12-01', 900)) AS v (AccountPeriod, BudgetAmount)
CROSS JOIN dbo.Ship AS s
CROSS JOIN dbo.Account AS a
WHERE s.ShipCode = 'SHIP01' AND a.AccountNumber = '7135000';

-- Actuals: 2024-01 .. 2026-08, 1-3 transactions per period, occasional zero-value transactions.
INSERT INTO dbo.AccountTransaction (ShipId, AccountId, AccountPeriod, ActualAmount, TransactionDate, Reference)
SELECT s.ShipId, a.AccountId, m.AccountPeriod,
       CASE WHEN (hh.Seed + t.TxnNo) % 23 = 0 THEN 0
            ELSE ROUND(p.ActualBase * sp.Factor * (1 + (YEAR(m.AccountPeriod) - 2024) * 0.03)
                       * (0.80 + ((hh.Seed / 7 + t.TxnNo * 13) % 41) / 100.0) / tc.TxnCount, 2)
       END,
       DATEADD(DAY, (hh.Seed / 3 + t.TxnNo * 7) % 28, m.AccountPeriod),
       CONCAT(N'AP-', s.ShipCode, N'-', CONVERT(CHAR(6), m.AccountPeriod, 112), N'-', a.AccountNumber, N'-', t.TxnNo)
FROM @ShipPlan AS sp
INNER JOIN dbo.Ship AS s ON s.ShipCode = sp.ShipCode
CROSS JOIN @Plan AS p
INNER JOIN dbo.Account AS a ON a.AccountNumber = p.AccountNumber
CROSS JOIN @Month AS m
CROSS APPLY (SELECT Seed = CHECKSUM(s.ShipCode, a.AccountNumber, m.AccountPeriod) & 0x7FFFFFFF) AS hh
CROSS APPLY (SELECT TxnCount = 1 + hh.Seed % 3) AS tc
CROSS JOIN (VALUES (1), (2), (3)) AS t (TxnNo)
WHERE m.AccountPeriod <= '2026-08-01'
  AND t.TxnNo <= tc.TxnCount
  AND (p.ActualPattern = 'M'
       OR (p.ActualPattern = 'S' AND MONTH(m.AccountPeriod) IN (6, 12))
       OR (p.ActualPattern = 'O' AND hh.Seed % 4 = 0))
  AND NOT (s.ShipCode = 'SHIP01' AND a.AccountNumber = '7135000' AND m.AccountPeriod = '2025-01-01');

-- The brief's three SHIP01 / 7135000 transactions for 2025-01 (300 + 0 + 700 = 1000).
INSERT INTO dbo.AccountTransaction (ShipId, AccountId, AccountPeriod, ActualAmount, TransactionDate, Reference)
SELECT s.ShipId, a.AccountId, '2025-01-01', v.ActualAmount, v.TransactionDate, v.Reference
FROM (VALUES (300, '2025-01-10', N'AP-SHIP01-202501-7135000-1'),
             (0,   '2025-01-17', N'AP-SHIP01-202501-7135000-2'),
             (700, '2025-01-24', N'AP-SHIP01-202501-7135000-3')) AS v (ActualAmount, TransactionDate, Reference)
CROSS JOIN dbo.Ship AS s
CROSS JOIN dbo.Account AS a
WHERE s.ShipCode = 'SHIP01' AND a.AccountNumber = '7135000';

/* ============================== Users and ship assignments ============================== */
INSERT INTO dbo.AppUser (FullName, Email, RoleId)
SELECT v.FullName, v.Email, r.RoleId
FROM (VALUES
        (N'Alex Morgan',   N'alex.morgan@example.com',   'Administrator'),
        (N'Priya Nair',    N'priya.nair@example.com',    'CrewingOfficer'),
        (N'Daniel Chen',   N'daniel.chen@example.com',   'Accountant'),
        (N'Sofia Andreou', N'sofia.andreou@example.com', 'OwnerRepresentative'),
        (N'Marco Rossi',   N'marco.rossi@example.com',   'Superintendent'),
        (N'Hannah Lee',    N'hannah.lee@example.com',    'FleetManager')
     ) AS v (FullName, Email, RoleName)
INNER JOIN dbo.AppRole AS r ON r.RoleName = v.RoleName;

DECLARE @AdminId INT = (SELECT UserId FROM dbo.AppUser WHERE Email = N'alex.morgan@example.com');

INSERT INTO dbo.UserShip (UserId, ShipId, AssignedByUserId)
SELECT u.UserId, s.ShipId, @AdminId
FROM (VALUES
        (N'priya.nair@example.com',    'SHIP01'), (N'priya.nair@example.com', 'SHIP02'), (N'priya.nair@example.com', 'SHIP04'),
        (N'daniel.chen@example.com',   'SHIP02'), (N'daniel.chen@example.com', 'SHIP03'),
        (N'sofia.andreou@example.com', 'SHIP01'),
        (N'marco.rossi@example.com',   'SHIP03'), (N'marco.rossi@example.com', 'SHIP05'),
        (N'hannah.lee@example.com',    'SHIP01'), (N'hannah.lee@example.com', 'SHIP02'), (N'hannah.lee@example.com', 'SHIP03')
     ) AS v (Email, ShipCode)
INNER JOIN dbo.AppUser AS u ON u.Email = v.Email
INNER JOIN dbo.Ship AS s ON s.ShipCode = v.ShipCode;

INSERT INTO dbo.AuditLog (ActorUserId, Action, EntityType, EntityKey, Details)
VALUES (NULL, 'SAMPLE_DATA_LOADED', 'Database', N'08_sample_data.sql', CONCAT(N'seedDate=', CONVERT(CHAR(10), @SeedDate, 23)));

COMMIT TRANSACTION;

PRINT 'Sample data loaded.';
