/*M!999999\- enable the sandbox mode */ 
-- MariaDB dump 10.19-11.7.2-MariaDB, for debian-linux-gnu (x86_64)
--
-- Host: localhost    Database: proxeeus_db
-- ------------------------------------------------------
-- Server version	11.7.2-MariaDB-ubu2404

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8mb4 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*M!100616 SET @OLD_NOTE_VERBOSITY=@@NOTE_VERBOSITY, NOTE_VERBOSITY=0 */;

--
-- Table structure for table `aa_ability`
--

DROP TABLE IF EXISTS `aa_ability`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `aa_ability` (
  `id` int(10) unsigned NOT NULL,
  `name` text NOT NULL,
  `category` int(10) NOT NULL DEFAULT -1,
  `classes` int(10) NOT NULL DEFAULT 65535,
  `races` int(10) NOT NULL DEFAULT 65535,
  `drakkin_heritage` int(10) NOT NULL DEFAULT 127,
  `deities` int(10) NOT NULL DEFAULT 131071,
  `status` int(10) NOT NULL DEFAULT 0,
  `type` int(10) NOT NULL DEFAULT 0,
  `charges` int(11) NOT NULL DEFAULT 0,
  `grant_only` tinyint(4) NOT NULL DEFAULT 0,
  `first_rank_id` int(10) NOT NULL DEFAULT -1,
  `enabled` tinyint(3) unsigned NOT NULL DEFAULT 1,
  `reset_on_death` tinyint(4) NOT NULL DEFAULT 0,
  `auto_grant_enabled` tinyint(4) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `aa_actions`
--

DROP TABLE IF EXISTS `aa_actions`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `aa_actions` (
  `aaid` mediumint(8) unsigned NOT NULL DEFAULT 0,
  `rank` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `reuse_time` mediumint(8) unsigned NOT NULL DEFAULT 0,
  `spell_id` mediumint(8) unsigned NOT NULL DEFAULT 0,
  `target` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `nonspell_action` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `nonspell_mana` mediumint(8) unsigned NOT NULL DEFAULT 0,
  `nonspell_duration` mediumint(8) unsigned NOT NULL DEFAULT 0,
  `redux_aa` mediumint(8) unsigned NOT NULL DEFAULT 0,
  `redux_rate` tinyint(4) NOT NULL DEFAULT 0,
  `redux_aa2` mediumint(8) unsigned NOT NULL DEFAULT 0,
  `redux_rate2` tinyint(4) NOT NULL DEFAULT 0,
  PRIMARY KEY (`aaid`,`rank`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `aa_effects`
--

DROP TABLE IF EXISTS `aa_effects`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `aa_effects` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `aaid` mediumint(8) unsigned NOT NULL DEFAULT 0,
  `slot` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `effectid` mediumint(8) unsigned NOT NULL DEFAULT 0,
  `base1` mediumint(8) unsigned NOT NULL DEFAULT 0,
  `base2` mediumint(8) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `NewIndex` (`aaid`,`slot`)
) ENGINE=InnoDB AUTO_INCREMENT=2377 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `aa_rank_effects`
--

DROP TABLE IF EXISTS `aa_rank_effects`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `aa_rank_effects` (
  `rank_id` int(10) unsigned NOT NULL,
  `slot` int(10) unsigned NOT NULL DEFAULT 1,
  `effect_id` int(10) NOT NULL DEFAULT 0,
  `base1` int(10) NOT NULL DEFAULT 0,
  `base2` int(10) NOT NULL DEFAULT 0,
  PRIMARY KEY (`rank_id`,`slot`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `aa_rank_prereqs`
--

DROP TABLE IF EXISTS `aa_rank_prereqs`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `aa_rank_prereqs` (
  `rank_id` int(10) unsigned NOT NULL,
  `aa_id` int(10) NOT NULL,
  `points` int(10) NOT NULL,
  PRIMARY KEY (`rank_id`,`aa_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `aa_ranks`
--

DROP TABLE IF EXISTS `aa_ranks`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `aa_ranks` (
  `id` int(10) unsigned NOT NULL,
  `upper_hotkey_sid` int(10) NOT NULL DEFAULT -1,
  `lower_hotkey_sid` int(10) NOT NULL DEFAULT -1,
  `title_sid` int(10) NOT NULL DEFAULT -1,
  `desc_sid` int(10) NOT NULL DEFAULT -1,
  `cost` int(10) NOT NULL DEFAULT 1,
  `level_req` int(10) NOT NULL DEFAULT 51,
  `spell` int(10) NOT NULL DEFAULT -1,
  `spell_type` int(10) NOT NULL DEFAULT 0,
  `recast_time` int(10) NOT NULL DEFAULT 0,
  `expansion` int(10) NOT NULL DEFAULT 0,
  `prev_id` int(10) NOT NULL DEFAULT -1,
  `next_id` int(10) NOT NULL DEFAULT -1,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `aa_required_level_cost`
--

DROP TABLE IF EXISTS `aa_required_level_cost`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `aa_required_level_cost` (
  `skill_id` int(10) unsigned NOT NULL,
  `level` int(10) unsigned NOT NULL,
  `cost` int(10) unsigned NOT NULL DEFAULT 0,
  `description` varchar(64) DEFAULT NULL COMMENT 'For reference only',
  PRIMARY KEY (`skill_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `aa_swarmpets`
--

DROP TABLE IF EXISTS `aa_swarmpets`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `aa_swarmpets` (
  `spell_id` mediumint(8) unsigned NOT NULL DEFAULT 0,
  `count` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `npc_id` int(11) NOT NULL DEFAULT 0,
  `duration` mediumint(8) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`spell_id`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `aa_timers`
--

DROP TABLE IF EXISTS `aa_timers`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `aa_timers` (
  `charid` int(12) unsigned NOT NULL DEFAULT 0,
  `ability` smallint(5) unsigned NOT NULL DEFAULT 0,
  `begin` int(10) unsigned NOT NULL DEFAULT 0,
  `end` int(10) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`charid`,`ability`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `account`
--

DROP TABLE IF EXISTS `account`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `account` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(30) NOT NULL DEFAULT '',
  `charname` varchar(64) NOT NULL DEFAULT '',
  `auto_login_charname` varchar(64) CHARACTER SET latin1 COLLATE latin1_swedish_ci NOT NULL DEFAULT '',
  `sharedplat` int(11) unsigned NOT NULL DEFAULT 0,
  `password` varchar(50) NOT NULL DEFAULT '',
  `status` int(5) NOT NULL DEFAULT 0,
  `ls_id` varchar(64) DEFAULT 'eqemu',
  `lsaccount_id` int(10) unsigned DEFAULT NULL,
  `gmspeed` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `invulnerable` tinyint(4) DEFAULT 0,
  `flymode` tinyint(4) DEFAULT 0,
  `ignore_tells` tinyint(4) DEFAULT 0,
  `revoked` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `karma` int(5) unsigned NOT NULL DEFAULT 0,
  `minilogin_ip` varchar(32) NOT NULL DEFAULT '',
  `hideme` tinyint(4) NOT NULL DEFAULT 0,
  `rulesflag` tinyint(1) unsigned NOT NULL DEFAULT 0,
  `suspendeduntil` datetime DEFAULT NULL,
  `time_creation` int(10) unsigned NOT NULL DEFAULT 0,
  `ban_reason` text DEFAULT NULL,
  `suspend_reason` text DEFAULT NULL,
  `crc_eqgame` text DEFAULT NULL,
  `crc_skillcaps` text DEFAULT NULL,
  `crc_basedata` text DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `name_ls_id` (`name`,`ls_id`),
  UNIQUE KEY `ls_id_lsaccount_id` (`ls_id`,`lsaccount_id`)
) ENGINE=InnoDB AUTO_INCREMENT=28 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `account_flags`
--

DROP TABLE IF EXISTS `account_flags`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `account_flags` (
  `p_accid` int(10) unsigned NOT NULL,
  `p_flag` varchar(50) NOT NULL,
  `p_value` varchar(80) NOT NULL,
  PRIMARY KEY (`p_accid`,`p_flag`),
  KEY `p_accid` (`p_accid`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `account_ip`
--

DROP TABLE IF EXISTS `account_ip`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `account_ip` (
  `accid` int(11) NOT NULL DEFAULT 0,
  `ip` varchar(32) NOT NULL DEFAULT '',
  `count` int(11) NOT NULL DEFAULT 1,
  `lastused` timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  UNIQUE KEY `ip` (`accid`,`ip`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `account_rewards`
--

DROP TABLE IF EXISTS `account_rewards`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `account_rewards` (
  `account_id` int(10) unsigned NOT NULL DEFAULT 0,
  `reward_id` int(10) unsigned NOT NULL DEFAULT 0,
  `amount` int(10) unsigned NOT NULL DEFAULT 0,
  UNIQUE KEY `account_reward` (`account_id`,`reward_id`),
  KEY `account_id` (`account_id`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `adventure_details`
--

DROP TABLE IF EXISTS `adventure_details`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `adventure_details` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `adventure_id` smallint(5) unsigned NOT NULL DEFAULT 0,
  `instance_id` int(11) NOT NULL DEFAULT -1,
  `count` smallint(5) unsigned NOT NULL DEFAULT 0,
  `assassinate_count` smallint(5) unsigned NOT NULL DEFAULT 0,
  `status` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `time_created` int(10) unsigned NOT NULL DEFAULT 0,
  `time_zoned` int(10) unsigned NOT NULL DEFAULT 0,
  `time_completed` int(10) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `adventure_members`
--

DROP TABLE IF EXISTS `adventure_members`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `adventure_members` (
  `id` int(10) unsigned NOT NULL DEFAULT 0,
  `charid` int(10) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`charid`),
  KEY `id` (`id`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `adventure_stats`
--

DROP TABLE IF EXISTS `adventure_stats`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `adventure_stats` (
  `player_id` int(10) unsigned NOT NULL DEFAULT 0,
  `guk_wins` mediumint(8) unsigned NOT NULL DEFAULT 0,
  `mir_wins` mediumint(8) unsigned NOT NULL DEFAULT 0,
  `mmc_wins` mediumint(8) unsigned NOT NULL DEFAULT 0,
  `ruj_wins` mediumint(8) unsigned NOT NULL DEFAULT 0,
  `tak_wins` mediumint(8) unsigned NOT NULL DEFAULT 0,
  `guk_losses` mediumint(8) unsigned NOT NULL DEFAULT 0,
  `mir_losses` mediumint(8) unsigned NOT NULL DEFAULT 0,
  `mmc_losses` mediumint(8) unsigned NOT NULL DEFAULT 0,
  `ruj_losses` mediumint(8) unsigned NOT NULL DEFAULT 0,
  `tak_losses` mediumint(8) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`player_id`),
  UNIQUE KEY `player_id` (`player_id`),
  KEY `player_id_2` (`player_id`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `adventure_template`
--

DROP TABLE IF EXISTS `adventure_template`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `adventure_template` (
  `id` int(10) unsigned NOT NULL,
  `zone` varchar(64) NOT NULL,
  `zone_version` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `is_hard` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `is_raid` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `min_level` tinyint(3) unsigned NOT NULL DEFAULT 1,
  `max_level` tinyint(3) unsigned NOT NULL DEFAULT 65,
  `type` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `type_data` int(10) unsigned NOT NULL DEFAULT 0,
  `type_count` smallint(5) unsigned NOT NULL DEFAULT 0,
  `assa_x` float NOT NULL DEFAULT 0,
  `assa_y` float NOT NULL DEFAULT 0,
  `assa_z` float NOT NULL DEFAULT 0,
  `assa_h` float NOT NULL DEFAULT 0,
  `text` varchar(511) DEFAULT NULL,
  `duration` int(10) unsigned NOT NULL DEFAULT 7200,
  `zone_in_time` int(10) unsigned NOT NULL DEFAULT 1800,
  `win_points` smallint(5) unsigned NOT NULL DEFAULT 0,
  `lose_points` smallint(5) unsigned NOT NULL DEFAULT 0,
  `theme` tinyint(3) unsigned NOT NULL DEFAULT 1,
  `zone_in_zone_id` smallint(5) unsigned NOT NULL DEFAULT 0,
  `zone_in_x` float NOT NULL DEFAULT 0,
  `zone_in_y` float NOT NULL DEFAULT 0,
  `zone_in_object_id` smallint(4) NOT NULL DEFAULT 0,
  `dest_x` float NOT NULL DEFAULT 0,
  `dest_y` float NOT NULL DEFAULT 0,
  `dest_z` float NOT NULL DEFAULT 0,
  `dest_h` float NOT NULL DEFAULT 0,
  `graveyard_zone_id` int(10) unsigned NOT NULL DEFAULT 0,
  `graveyard_x` float NOT NULL DEFAULT 0,
  `graveyard_y` float NOT NULL DEFAULT 0,
  `graveyard_z` float NOT NULL DEFAULT 0,
  `graveyard_radius` float unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `id` (`id`),
  KEY `id_2` (`id`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `adventure_template_entry`
--

DROP TABLE IF EXISTS `adventure_template_entry`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `adventure_template_entry` (
  `id` int(10) unsigned NOT NULL DEFAULT 0,
  `template_id` int(10) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`,`template_id`),
  KEY `id` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `adventure_template_entry_flavor`
--

DROP TABLE IF EXISTS `adventure_template_entry_flavor`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `adventure_template_entry_flavor` (
  `id` int(10) unsigned NOT NULL DEFAULT 0,
  `text` text NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `id` (`id`),
  KEY `id_2` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `altadv_vars`
--

DROP TABLE IF EXISTS `altadv_vars`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `altadv_vars` (
  `skill_id` int(11) NOT NULL DEFAULT 0,
  `name` varchar(128) DEFAULT NULL,
  `cost` int(11) DEFAULT NULL,
  `max_level` int(11) DEFAULT NULL,
  `hotkey_sid` int(10) unsigned NOT NULL DEFAULT 0,
  `hotkey_sid2` int(10) unsigned NOT NULL DEFAULT 0,
  `title_sid` int(10) unsigned NOT NULL DEFAULT 0,
  `desc_sid` int(10) unsigned NOT NULL DEFAULT 0,
  `type` tinyint(3) unsigned NOT NULL DEFAULT 1,
  `spellid` int(10) unsigned NOT NULL DEFAULT 0,
  `prereq_skill` int(10) unsigned NOT NULL DEFAULT 0,
  `prereq_minpoints` int(10) unsigned NOT NULL DEFAULT 0,
  `spell_type` int(10) unsigned NOT NULL DEFAULT 0,
  `spell_refresh` int(10) unsigned NOT NULL DEFAULT 0,
  `classes` int(10) unsigned NOT NULL DEFAULT 65534,
  `berserker` int(10) unsigned NOT NULL DEFAULT 0,
  `class_type` int(10) unsigned NOT NULL DEFAULT 0,
  `cost_inc` tinyint(4) NOT NULL DEFAULT 0,
  `aa_expansion` smallint(3) unsigned NOT NULL DEFAULT 3,
  `special_category` int(10) unsigned NOT NULL DEFAULT 4294967295,
  `sof_type` tinyint(3) unsigned NOT NULL DEFAULT 1,
  `sof_cost_inc` tinyint(3) NOT NULL DEFAULT 0,
  `sof_max_level` tinyint(3) unsigned NOT NULL DEFAULT 1,
  `sof_next_skill` int(10) unsigned NOT NULL DEFAULT 0,
  `clientver` tinyint(3) unsigned NOT NULL DEFAULT 1,
  `account_time_required` int(10) unsigned NOT NULL DEFAULT 0,
  `sof_current_level` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `sof_next_id` int(10) unsigned NOT NULL DEFAULT 0,
  `level_inc` tinyint(3) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`skill_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `alternate_currency`
--

DROP TABLE IF EXISTS `alternate_currency`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `alternate_currency` (
  `id` int(10) NOT NULL,
  `item_id` int(10) NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `auras`
--

DROP TABLE IF EXISTS `auras`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `auras` (
  `type` int(10) NOT NULL,
  `npc_type` int(10) NOT NULL,
  `name` varchar(64) NOT NULL,
  `spell_id` int(10) NOT NULL,
  `distance` int(10) NOT NULL DEFAULT 60,
  `aura_type` int(10) NOT NULL DEFAULT 1,
  `spawn_type` int(10) NOT NULL DEFAULT 0,
  `movement` int(10) NOT NULL DEFAULT 0,
  `duration` int(10) NOT NULL DEFAULT 5400,
  `icon` int(10) NOT NULL DEFAULT -1,
  `cast_time` int(10) NOT NULL DEFAULT 0,
  PRIMARY KEY (`type`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `banned_ips`
--

DROP TABLE IF EXISTS `banned_ips`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `banned_ips` (
  `ip_address` varchar(32) CHARACTER SET utf8mb3 COLLATE utf8mb3_unicode_ci NOT NULL DEFAULT '',
  `notes` varchar(32) CHARACTER SET utf8mb3 COLLATE utf8mb3_unicode_ci DEFAULT NULL,
  PRIMARY KEY (`ip_address`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `base_data`
--

DROP TABLE IF EXISTS `base_data`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `base_data` (
  `level` tinyint(3) unsigned NOT NULL,
  `class` tinyint(2) unsigned NOT NULL,
  `hp` double NOT NULL,
  `mana` double NOT NULL,
  `end` double NOT NULL,
  `hp_regen` double NOT NULL,
  `end_regen` double NOT NULL,
  `hp_fac` double NOT NULL,
  `mana_fac` double NOT NULL,
  `end_fac` double NOT NULL,
  PRIMARY KEY (`level`,`class`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `blocked_spells`
--

DROP TABLE IF EXISTS `blocked_spells`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `blocked_spells` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `spellid` mediumint(8) unsigned NOT NULL DEFAULT 0,
  `type` tinyint(4) NOT NULL DEFAULT 0,
  `zoneid` int(4) NOT NULL DEFAULT 0,
  `x` float NOT NULL DEFAULT 0,
  `y` float NOT NULL DEFAULT 0,
  `z` float NOT NULL DEFAULT 0,
  `x_diff` float NOT NULL DEFAULT 0,
  `y_diff` float NOT NULL DEFAULT 0,
  `z_diff` float NOT NULL DEFAULT 0,
  `message` varchar(255) NOT NULL DEFAULT 'You cannot cast that spell here',
  `description` varchar(255) DEFAULT NULL,
  `min_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `max_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `content_flags` varchar(100) CHARACTER SET latin1 COLLATE latin1_swedish_ci DEFAULT NULL,
  `content_flags_disabled` varchar(100) CHARACTER SET latin1 COLLATE latin1_swedish_ci DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `books`
--

DROP TABLE IF EXISTS `books`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `books` (
  `id` int(20) NOT NULL AUTO_INCREMENT,
  `name` varchar(30) NOT NULL DEFAULT '',
  `txtfile` text NOT NULL,
  `language` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `filename` (`name`)
) ENGINE=MyISAM AUTO_INCREMENT=851 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `bot_buffs`
--

DROP TABLE IF EXISTS `bot_buffs`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `bot_buffs` (
  `buffs_index` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `bot_id` int(11) unsigned NOT NULL DEFAULT 0,
  `spell_id` int(10) unsigned NOT NULL DEFAULT 0,
  `caster_level` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `duration_formula` int(10) unsigned NOT NULL DEFAULT 0,
  `tics_remaining` int(11) unsigned NOT NULL DEFAULT 0,
  `poison_counters` int(11) unsigned NOT NULL DEFAULT 0,
  `disease_counters` int(11) unsigned NOT NULL DEFAULT 0,
  `curse_counters` int(11) unsigned NOT NULL DEFAULT 0,
  `corruption_counters` int(11) unsigned NOT NULL DEFAULT 0,
  `numhits` int(10) unsigned NOT NULL DEFAULT 0,
  `melee_rune` int(10) unsigned NOT NULL DEFAULT 0,
  `magic_rune` int(10) unsigned NOT NULL DEFAULT 0,
  `dot_rune` int(10) unsigned NOT NULL DEFAULT 0,
  `persistent` tinyint(1) NOT NULL DEFAULT 0,
  `caston_x` int(10) NOT NULL DEFAULT 0,
  `caston_y` int(10) NOT NULL DEFAULT 0,
  `caston_z` int(10) NOT NULL DEFAULT 0,
  `extra_di_chance` int(10) unsigned NOT NULL DEFAULT 0,
  `instrument_mod` int(10) NOT NULL DEFAULT 10,
  PRIMARY KEY (`buffs_index`),
  KEY `FK_bot_buffs_1` (`bot_id`)
) ENGINE=InnoDB AUTO_INCREMENT=577551 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `bot_command_settings`
--

DROP TABLE IF EXISTS `bot_command_settings`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `bot_command_settings` (
  `bot_command` varchar(128) NOT NULL DEFAULT '',
  `access` int(11) NOT NULL DEFAULT 0,
  `aliases` varchar(256) NOT NULL DEFAULT '',
  PRIMARY KEY (`bot_command`),
  UNIQUE KEY `UK_bot_command_settings_1` (`bot_command`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `bot_create_combinations`
--

DROP TABLE IF EXISTS `bot_create_combinations`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `bot_create_combinations` (
  `race` int(10) unsigned NOT NULL DEFAULT 0,
  `classes` int(10) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`race`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci ROW_FORMAT=COMPACT;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `bot_data`
--

DROP TABLE IF EXISTS `bot_data`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `bot_data` (
  `bot_id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `owner_id` int(11) unsigned NOT NULL,
  `spells_id` int(11) unsigned NOT NULL DEFAULT 0,
  `name` varchar(64) CHARACTER SET utf8mb3 COLLATE utf8mb3_unicode_ci NOT NULL DEFAULT '',
  `last_name` varchar(64) CHARACTER SET utf8mb3 COLLATE utf8mb3_unicode_ci NOT NULL DEFAULT '',
  `title` varchar(32) CHARACTER SET utf8mb3 COLLATE utf8mb3_unicode_ci NOT NULL DEFAULT '',
  `suffix` varchar(32) CHARACTER SET utf8mb3 COLLATE utf8mb3_unicode_ci NOT NULL DEFAULT '',
  `zone_id` smallint(6) NOT NULL DEFAULT 0,
  `gender` tinyint(2) NOT NULL DEFAULT 0,
  `race` smallint(5) NOT NULL DEFAULT 0,
  `class` tinyint(2) NOT NULL DEFAULT 0,
  `level` tinyint(2) unsigned NOT NULL DEFAULT 0,
  `deity` int(11) unsigned NOT NULL DEFAULT 0,
  `creation_day` int(11) unsigned NOT NULL DEFAULT 0,
  `last_spawn` int(11) unsigned NOT NULL DEFAULT 0,
  `time_spawned` int(11) unsigned NOT NULL DEFAULT 0,
  `size` float NOT NULL DEFAULT 0,
  `face` int(10) NOT NULL DEFAULT 1,
  `hair_color` int(10) NOT NULL DEFAULT 1,
  `hair_style` int(10) NOT NULL DEFAULT 1,
  `beard` int(10) NOT NULL DEFAULT 0,
  `beard_color` int(10) NOT NULL DEFAULT 1,
  `eye_color_1` int(10) NOT NULL DEFAULT 1,
  `eye_color_2` int(10) NOT NULL DEFAULT 1,
  `drakkin_heritage` int(10) NOT NULL DEFAULT 0,
  `drakkin_tattoo` int(10) NOT NULL DEFAULT 0,
  `drakkin_details` int(10) NOT NULL DEFAULT 0,
  `ac` smallint(5) NOT NULL DEFAULT 0,
  `atk` mediumint(9) NOT NULL DEFAULT 0,
  `hp` int(11) NOT NULL DEFAULT 0,
  `mana` int(11) NOT NULL DEFAULT 0,
  `str` mediumint(8) NOT NULL DEFAULT 75,
  `sta` mediumint(8) NOT NULL DEFAULT 75,
  `cha` mediumint(8) NOT NULL DEFAULT 75,
  `dex` mediumint(8) NOT NULL DEFAULT 75,
  `int` mediumint(8) NOT NULL DEFAULT 75,
  `agi` mediumint(8) NOT NULL DEFAULT 75,
  `wis` mediumint(8) NOT NULL DEFAULT 75,
  `extra_haste` mediumint(8) NOT NULL DEFAULT 0,
  `fire` smallint(5) NOT NULL DEFAULT 0,
  `cold` smallint(5) NOT NULL DEFAULT 0,
  `magic` smallint(5) NOT NULL DEFAULT 0,
  `poison` smallint(5) NOT NULL DEFAULT 0,
  `disease` smallint(5) NOT NULL DEFAULT 0,
  `corruption` smallint(5) NOT NULL DEFAULT 0,
  `show_helm` int(11) unsigned NOT NULL DEFAULT 0,
  `follow_distance` int(11) unsigned NOT NULL DEFAULT 200,
  `stop_melee_level` tinyint(3) unsigned NOT NULL DEFAULT 255,
  `expansion_bitmask` int(11) NOT NULL DEFAULT -1,
  `enforce_spell_settings` tinyint(2) unsigned NOT NULL DEFAULT 0,
  `archery_setting` tinyint(2) unsigned NOT NULL DEFAULT 0,
  `caster_range` int(11) unsigned NOT NULL DEFAULT 300,
  PRIMARY KEY (`bot_id`)
) ENGINE=InnoDB AUTO_INCREMENT=608 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `bot_guilds`
--

DROP TABLE IF EXISTS `bot_guilds`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `bot_guilds` (
  `bot_id` int(11) NOT NULL,
  `guild_id` int(11) NOT NULL,
  PRIMARY KEY (`bot_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `bot_heal_rotation_members`
--

DROP TABLE IF EXISTS `bot_heal_rotation_members`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `bot_heal_rotation_members` (
  `member_index` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `heal_rotation_index` int(11) unsigned NOT NULL DEFAULT 0,
  `bot_id` int(11) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`member_index`),
  KEY `FK_bot_heal_rotation_members_1` (`heal_rotation_index`),
  KEY `FK_bot_heal_rotation_members_2` (`bot_id`)
) ENGINE=InnoDB AUTO_INCREMENT=27 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `bot_heal_rotation_targets`
--

DROP TABLE IF EXISTS `bot_heal_rotation_targets`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `bot_heal_rotation_targets` (
  `target_index` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `heal_rotation_index` int(11) unsigned NOT NULL DEFAULT 0,
  `target_name` varchar(64) NOT NULL DEFAULT '',
  PRIMARY KEY (`target_index`),
  KEY `FK_bot_heal_rotation_targets` (`heal_rotation_index`)
) ENGINE=InnoDB AUTO_INCREMENT=4 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `bot_heal_rotations`
--

DROP TABLE IF EXISTS `bot_heal_rotations`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `bot_heal_rotations` (
  `heal_rotation_index` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `bot_id` int(11) unsigned NOT NULL DEFAULT 0,
  `interval` int(11) unsigned NOT NULL DEFAULT 0,
  `fast_heals` int(3) unsigned NOT NULL DEFAULT 0,
  `adaptive_targeting` int(3) unsigned NOT NULL DEFAULT 0,
  `casting_override` int(3) unsigned NOT NULL DEFAULT 0,
  `safe_hp_base` float unsigned NOT NULL DEFAULT 0,
  `safe_hp_cloth` float unsigned NOT NULL DEFAULT 0,
  `safe_hp_leather` float unsigned NOT NULL DEFAULT 0,
  `safe_hp_chain` float unsigned NOT NULL DEFAULT 0,
  `safe_hp_plate` float unsigned NOT NULL DEFAULT 0,
  `critical_hp_base` float unsigned NOT NULL DEFAULT 0,
  `critical_hp_cloth` float unsigned NOT NULL DEFAULT 0,
  `critical_hp_leather` float unsigned NOT NULL DEFAULT 0,
  `critical_hp_chain` float unsigned NOT NULL DEFAULT 0,
  `critical_hp_plate` float unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`heal_rotation_index`),
  KEY `FK_bot_heal_rotations` (`bot_id`)
) ENGINE=InnoDB AUTO_INCREMENT=5 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `bot_inspect_messages`
--

DROP TABLE IF EXISTS `bot_inspect_messages`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `bot_inspect_messages` (
  `bot_id` int(11) unsigned NOT NULL,
  `inspect_message` varchar(256) NOT NULL DEFAULT '',
  PRIMARY KEY (`bot_id`),
  KEY `bot_id` (`bot_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `bot_inventories`
--

DROP TABLE IF EXISTS `bot_inventories`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `bot_inventories` (
  `inventories_index` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `bot_id` int(11) unsigned NOT NULL DEFAULT 0,
  `slot_id` mediumint(7) unsigned NOT NULL DEFAULT 0,
  `item_id` int(11) unsigned DEFAULT 0,
  `inst_charges` smallint(3) unsigned DEFAULT 0,
  `inst_color` int(11) unsigned NOT NULL DEFAULT 0,
  `inst_no_drop` tinyint(1) unsigned NOT NULL DEFAULT 0,
  `inst_custom_data` text DEFAULT NULL,
  `ornament_icon` int(11) unsigned NOT NULL DEFAULT 0,
  `ornament_id_file` int(11) unsigned NOT NULL DEFAULT 0,
  `ornament_hero_model` int(11) NOT NULL DEFAULT 0,
  `augment_1` mediumint(7) unsigned NOT NULL DEFAULT 0,
  `augment_2` mediumint(7) unsigned NOT NULL DEFAULT 0,
  `augment_3` mediumint(7) unsigned NOT NULL DEFAULT 0,
  `augment_4` mediumint(7) unsigned NOT NULL DEFAULT 0,
  `augment_5` mediumint(7) unsigned NOT NULL DEFAULT 0,
  `augment_6` mediumint(7) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`inventories_index`),
  KEY `FK_bot_inventories_1` (`bot_id`)
) ENGINE=InnoDB AUTO_INCREMENT=10140 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `bot_owner_options`
--

DROP TABLE IF EXISTS `bot_owner_options`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `bot_owner_options` (
  `owner_id` int(11) unsigned NOT NULL,
  `option_type` smallint(3) unsigned NOT NULL,
  `option_value` smallint(3) unsigned DEFAULT 0,
  PRIMARY KEY (`owner_id`,`option_type`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `bot_pet_buffs`
--

DROP TABLE IF EXISTS `bot_pet_buffs`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `bot_pet_buffs` (
  `pet_buffs_index` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `pets_index` int(10) unsigned NOT NULL DEFAULT 0,
  `spell_id` int(10) unsigned NOT NULL DEFAULT 0,
  `caster_level` int(10) unsigned NOT NULL DEFAULT 0,
  `duration` int(11) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`pet_buffs_index`),
  KEY `FK_bot_pet_buffs_1` (`pets_index`)
) ENGINE=InnoDB AUTO_INCREMENT=64562 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `bot_pet_inventories`
--

DROP TABLE IF EXISTS `bot_pet_inventories`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `bot_pet_inventories` (
  `pet_inventories_index` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `pets_index` int(10) unsigned NOT NULL DEFAULT 0,
  `item_id` int(10) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`pet_inventories_index`),
  KEY `FK_bot_pet_inventories_1` (`pets_index`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `bot_pets`
--

DROP TABLE IF EXISTS `bot_pets`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `bot_pets` (
  `pets_index` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `spell_id` int(10) unsigned NOT NULL DEFAULT 0,
  `bot_id` int(10) unsigned NOT NULL DEFAULT 0,
  `name` varchar(64) DEFAULT NULL,
  `mana` int(11) NOT NULL DEFAULT 0,
  `hp` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`pets_index`),
  UNIQUE KEY `U_bot_pets_1` (`bot_id`),
  KEY `FK_bot_pets_1` (`bot_id`)
) ENGINE=InnoDB AUTO_INCREMENT=13843 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `bot_spell_casting_chances`
--

DROP TABLE IF EXISTS `bot_spell_casting_chances`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `bot_spell_casting_chances` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `spell_type_index` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `class_id` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `stance_index` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `nHSND_value` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `pH_value` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `pS_value` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `pHS_value` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `pN_value` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `pHN_value` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `pSN_value` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `pHSN_value` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `pD_value` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `pHD_value` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `pSD_value` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `pHSD_value` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `pND_value` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `pHND_value` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `pSND_value` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `pHSND_value` tinyint(3) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `spelltype_class_stance` (`spell_type_index`,`class_id`,`stance_index`)
) ENGINE=InnoDB AUTO_INCREMENT=2466 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `bot_spell_settings`
--

DROP TABLE IF EXISTS `bot_spell_settings`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `bot_spell_settings` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `bot_id` int(11) NOT NULL DEFAULT 0,
  `spell_id` smallint(5) NOT NULL DEFAULT 0,
  `priority` smallint(5) NOT NULL DEFAULT 0,
  `min_hp` smallint(5) NOT NULL DEFAULT 0,
  `max_hp` smallint(5) NOT NULL DEFAULT 0,
  `is_enabled` tinyint(1) unsigned NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`) USING BTREE
) ENGINE=InnoDB AUTO_INCREMENT=91 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `bot_spells_entries`
--

DROP TABLE IF EXISTS `bot_spells_entries`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `bot_spells_entries` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `npc_spells_id` int(11) NOT NULL DEFAULT 0,
  `spellid` smallint(5) NOT NULL DEFAULT 0,
  `type` int(10) unsigned NOT NULL DEFAULT 0,
  `minlevel` tinyint(3) unsigned DEFAULT 0,
  `maxlevel` tinyint(3) unsigned NOT NULL DEFAULT 255,
  `manacost` smallint(5) NOT NULL DEFAULT -1,
  `recast_delay` int(11) NOT NULL DEFAULT -1,
  `priority` smallint(5) NOT NULL DEFAULT 0,
  `resist_adjust` int(11) NOT NULL DEFAULT 0,
  `min_hp` smallint(5) NOT NULL DEFAULT 0,
  `max_hp` smallint(5) NOT NULL DEFAULT 0,
  `bucket_name` varchar(100) NOT NULL DEFAULT '',
  `bucket_value` varchar(100) NOT NULL DEFAULT '',
  `bucket_comparison` tinyint(3) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=2745 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `bot_stances`
--

DROP TABLE IF EXISTS `bot_stances`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `bot_stances` (
  `bot_id` int(11) unsigned NOT NULL DEFAULT 0,
  `stance_id` tinyint(3) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`bot_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `bot_starting_items`
--

DROP TABLE IF EXISTS `bot_starting_items`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `bot_starting_items` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `races` int(11) unsigned NOT NULL DEFAULT 0,
  `classes` int(11) unsigned NOT NULL DEFAULT 0,
  `item_id` int(11) unsigned NOT NULL DEFAULT 0,
  `item_charges` tinyint(3) unsigned NOT NULL DEFAULT 1,
  `augment_one` int(11) unsigned NOT NULL DEFAULT 0,
  `augment_two` int(11) unsigned NOT NULL DEFAULT 0,
  `augment_three` int(11) unsigned NOT NULL DEFAULT 0,
  `augment_four` int(11) unsigned NOT NULL DEFAULT 0,
  `augment_five` int(11) unsigned NOT NULL DEFAULT 0,
  `augment_six` int(11) unsigned NOT NULL DEFAULT 0,
  `min_status` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `slot_id` mediumint(9) NOT NULL DEFAULT -1,
  `min_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `max_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `content_flags` varchar(100) DEFAULT NULL,
  `content_flags_disabled` varchar(100) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `bot_timers`
--

DROP TABLE IF EXISTS `bot_timers`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `bot_timers` (
  `bot_id` int(11) unsigned NOT NULL DEFAULT 0,
  `timer_id` int(11) unsigned NOT NULL DEFAULT 0,
  `timer_value` int(11) unsigned NOT NULL DEFAULT 0,
  `recast_time` int(11) unsigned NOT NULL DEFAULT 0,
  `is_spell` tinyint(2) unsigned NOT NULL DEFAULT 0,
  `is_disc` tinyint(2) unsigned NOT NULL DEFAULT 0,
  `spell_id` int(11) unsigned NOT NULL DEFAULT 0,
  `is_item` tinyint(2) unsigned NOT NULL DEFAULT 0,
  `item_id` int(11) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`bot_id`,`timer_id`,`spell_id`,`item_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `botbuffs___`
--

DROP TABLE IF EXISTS `botbuffs___`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `botbuffs___` (
  `BotBuffId` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `BotId` int(10) unsigned NOT NULL DEFAULT 0,
  `SpellId` int(10) unsigned NOT NULL DEFAULT 0,
  `CasterLevel` int(10) unsigned NOT NULL DEFAULT 0,
  `DurationFormula` int(10) unsigned NOT NULL DEFAULT 0,
  `TicsRemaining` int(11) unsigned NOT NULL DEFAULT 0,
  `PoisonCounters` int(11) unsigned NOT NULL DEFAULT 0,
  `DiseaseCounters` int(11) unsigned NOT NULL DEFAULT 0,
  `CurseCounters` int(11) unsigned NOT NULL DEFAULT 0,
  `CorruptionCounters` int(11) unsigned NOT NULL DEFAULT 0,
  `HitCount` int(10) unsigned NOT NULL DEFAULT 0,
  `MeleeRune` int(10) unsigned NOT NULL DEFAULT 0,
  `MagicRune` int(10) unsigned NOT NULL DEFAULT 0,
  `DeathSaveSuccessChance` int(10) unsigned NOT NULL DEFAULT 0,
  `CasterAARank` int(10) unsigned NOT NULL DEFAULT 0,
  `Persistent` tinyint(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`BotBuffId`),
  KEY `FK_botbuff_1` (`BotId`),
  CONSTRAINT `FK_botbuff_1` FOREIGN KEY (`BotId`) REFERENCES `bots_old` (`BotID`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `botbuffs_old`
--

DROP TABLE IF EXISTS `botbuffs_old`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `botbuffs_old` (
  `BotBuffId` int(10) unsigned NOT NULL,
  `BotId` int(10) unsigned NOT NULL DEFAULT 0,
  `SpellId` int(10) unsigned NOT NULL DEFAULT 0,
  `CasterLevel` int(10) unsigned NOT NULL DEFAULT 0,
  `DurationFormula` int(10) unsigned NOT NULL DEFAULT 0,
  `TicsRemaining` int(11) unsigned NOT NULL DEFAULT 0,
  `PoisonCounters` int(11) unsigned NOT NULL DEFAULT 0,
  `DiseaseCounters` int(11) unsigned NOT NULL DEFAULT 0,
  `CurseCounters` int(11) unsigned NOT NULL DEFAULT 0,
  `CorruptionCounters` int(11) unsigned NOT NULL DEFAULT 0,
  `HitCount` int(10) unsigned NOT NULL DEFAULT 0,
  `MeleeRune` int(10) unsigned NOT NULL DEFAULT 0,
  `MagicRune` int(10) unsigned NOT NULL DEFAULT 0,
  `dot_rune` int(10) unsigned NOT NULL DEFAULT 0,
  `caston_x` int(10) unsigned NOT NULL DEFAULT 0,
  `DeathSaveSuccessChance` int(10) unsigned NOT NULL DEFAULT 0,
  `CasterAARank` int(10) unsigned NOT NULL DEFAULT 0,
  `Persistent` tinyint(1) NOT NULL DEFAULT 0,
  `caston_y` int(10) unsigned NOT NULL DEFAULT 0,
  `caston_z` int(10) unsigned NOT NULL DEFAULT 0,
  `ExtraDIChance` int(10) unsigned NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `botgroup_old`
--

DROP TABLE IF EXISTS `botgroup_old`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `botgroup_old` (
  `BotGroupId` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `BotGroupLeaderBotId` int(10) unsigned NOT NULL DEFAULT 0,
  `BotGroupName` varchar(64) NOT NULL,
  PRIMARY KEY (`BotGroupId`),
  KEY `FK_botgroup_1` (`BotGroupLeaderBotId`),
  CONSTRAINT `FK_botgroup_1` FOREIGN KEY (`BotGroupLeaderBotId`) REFERENCES `bots_old` (`BotID`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `botgroupmembers_old`
--

DROP TABLE IF EXISTS `botgroupmembers_old`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `botgroupmembers_old` (
  `BotGroupMemberId` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `BotGroupId` int(10) unsigned NOT NULL DEFAULT 0,
  `BotId` int(10) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`BotGroupMemberId`),
  KEY `FK_botgroupmembers_1` (`BotGroupId`),
  KEY `FK_botgroupmembers_2` (`BotId`),
  CONSTRAINT `FK_botgroupmembers_1` FOREIGN KEY (`BotGroupId`) REFERENCES `botgroup_old` (`BotGroupId`),
  CONSTRAINT `FK_botgroupmembers_2` FOREIGN KEY (`BotId`) REFERENCES `bots_old` (`BotID`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `botguildmembers_old`
--

DROP TABLE IF EXISTS `botguildmembers_old`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `botguildmembers_old` (
  `char_id` int(11) NOT NULL DEFAULT 0,
  `guild_id` mediumint(8) unsigned NOT NULL DEFAULT 0,
  `rank` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `tribute_enable` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `total_tribute` int(10) unsigned NOT NULL DEFAULT 0,
  `last_tribute` int(10) unsigned NOT NULL DEFAULT 0,
  `banker` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `public_note` text DEFAULT NULL,
  `alt` tinyint(3) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`char_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `botinventory_old`
--

DROP TABLE IF EXISTS `botinventory_old`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `botinventory_old` (
  `BotInventoryID` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `BotID` int(10) unsigned NOT NULL DEFAULT 0,
  `SlotID` int(11) NOT NULL DEFAULT 0,
  `ItemID` int(10) unsigned NOT NULL DEFAULT 0,
  `charges` tinyint(3) unsigned DEFAULT 0,
  `color` int(10) unsigned NOT NULL DEFAULT 0,
  `augslot1` mediumint(7) unsigned NOT NULL DEFAULT 0,
  `augslot2` mediumint(7) unsigned NOT NULL DEFAULT 0,
  `augslot3` mediumint(7) unsigned NOT NULL DEFAULT 0,
  `augslot4` mediumint(7) unsigned NOT NULL DEFAULT 0,
  `augslot5` mediumint(7) unsigned DEFAULT 0,
  `instnodrop` tinyint(1) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`BotInventoryID`),
  KEY `FK_botinventory_1` (`BotID`),
  CONSTRAINT `FK_botinventory_1` FOREIGN KEY (`BotID`) REFERENCES `bots_old` (`BotID`)
) ENGINE=InnoDB AUTO_INCREMENT=6 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `botpetbuffs_old`
--

DROP TABLE IF EXISTS `botpetbuffs_old`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `botpetbuffs_old` (
  `BotPetBuffId` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `BotPetsId` int(10) unsigned NOT NULL DEFAULT 0,
  `SpellId` int(10) unsigned NOT NULL DEFAULT 0,
  `CasterLevel` int(10) unsigned NOT NULL DEFAULT 0,
  `Duration` int(11) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`BotPetBuffId`),
  KEY `FK_botpetbuffs_1` (`BotPetsId`),
  CONSTRAINT `FK_botpetbuffs_1` FOREIGN KEY (`BotPetsId`) REFERENCES `botpets_old` (`BotPetsId`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `botpetinventory_old`
--

DROP TABLE IF EXISTS `botpetinventory_old`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `botpetinventory_old` (
  `BotPetInventoryId` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `BotPetsId` int(10) unsigned NOT NULL DEFAULT 0,
  `ItemId` int(10) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`BotPetInventoryId`),
  KEY `FK_botpetinventory_1` (`BotPetsId`),
  CONSTRAINT `FK_botpetinventory_1` FOREIGN KEY (`BotPetsId`) REFERENCES `botpets_old` (`BotPetsId`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `botpets_old`
--

DROP TABLE IF EXISTS `botpets_old`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `botpets_old` (
  `BotPetsId` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `PetId` int(10) unsigned NOT NULL DEFAULT 0,
  `BotId` int(10) unsigned NOT NULL DEFAULT 0,
  `Name` varchar(64) DEFAULT NULL,
  `Mana` int(11) NOT NULL DEFAULT 0,
  `HitPoints` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`BotPetsId`),
  UNIQUE KEY `U_botpets_1` (`BotId`),
  KEY `FK_botpets_1` (`BotId`),
  CONSTRAINT `FK_botpets_1` FOREIGN KEY (`BotId`) REFERENCES `bots_old` (`BotID`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `bots_old`
--

DROP TABLE IF EXISTS `bots_old`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `bots_old` (
  `BotID` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `BotOwnerCharacterID` int(10) unsigned NOT NULL,
  `BotSpellsID` int(10) unsigned NOT NULL DEFAULT 0,
  `Name` varchar(64) NOT NULL,
  `LastName` varchar(32) DEFAULT NULL,
  `BotLevel` tinyint(2) unsigned NOT NULL DEFAULT 0,
  `Race` smallint(5) NOT NULL DEFAULT 0,
  `Class` tinyint(2) NOT NULL DEFAULT 0,
  `Gender` tinyint(2) NOT NULL DEFAULT 0,
  `Size` float NOT NULL DEFAULT 0,
  `Face` int(10) NOT NULL DEFAULT 1,
  `LuclinHairStyle` int(10) NOT NULL DEFAULT 1,
  `LuclinHairColor` int(10) NOT NULL DEFAULT 1,
  `LuclinEyeColor` int(10) NOT NULL DEFAULT 1,
  `LuclinEyeColor2` int(10) NOT NULL DEFAULT 1,
  `LuclinBeardColor` int(10) NOT NULL DEFAULT 1,
  `LuclinBeard` int(10) NOT NULL DEFAULT 0,
  `DrakkinHeritage` int(10) NOT NULL DEFAULT 0,
  `DrakkinTattoo` int(10) NOT NULL DEFAULT 0,
  `DrakkinDetails` int(10) NOT NULL DEFAULT 0,
  `HP` int(11) NOT NULL DEFAULT 0,
  `Mana` int(11) NOT NULL DEFAULT 0,
  `MR` smallint(5) NOT NULL DEFAULT 0,
  `CR` smallint(5) NOT NULL DEFAULT 0,
  `DR` smallint(5) NOT NULL DEFAULT 0,
  `FR` smallint(5) NOT NULL DEFAULT 0,
  `PR` smallint(5) NOT NULL DEFAULT 0,
  `Corrup` smallint(5) NOT NULL DEFAULT 0,
  `AC` smallint(5) NOT NULL DEFAULT 0,
  `STR` mediumint(8) NOT NULL DEFAULT 75,
  `STA` mediumint(8) NOT NULL DEFAULT 75,
  `DEX` mediumint(8) NOT NULL DEFAULT 75,
  `AGI` mediumint(8) NOT NULL DEFAULT 75,
  `_INT` mediumint(8) NOT NULL DEFAULT 80,
  `WIS` mediumint(8) NOT NULL DEFAULT 75,
  `CHA` mediumint(8) NOT NULL DEFAULT 75,
  `ATK` mediumint(9) NOT NULL DEFAULT 0,
  `BotCreateDate` timestamp NOT NULL DEFAULT current_timestamp(),
  `LastSpawnDate` datetime NOT NULL DEFAULT '0000-00-00 00:00:00',
  `TotalPlayTime` int(10) unsigned NOT NULL DEFAULT 0,
  `LastZoneId` smallint(6) NOT NULL DEFAULT 0,
  `BotInspectMessage` varchar(256) NOT NULL DEFAULT '',
  PRIMARY KEY (`BotID`)
) ENGINE=InnoDB AUTO_INCREMENT=16 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `botstances_old`
--

DROP TABLE IF EXISTS `botstances_old`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `botstances_old` (
  `BotID` int(10) unsigned NOT NULL DEFAULT 0,
  `StanceID` tinyint(3) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`BotID`),
  CONSTRAINT `FK_botstances_1` FOREIGN KEY (`BotID`) REFERENCES `bots_old` (`BotID`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `bottimers_old`
--

DROP TABLE IF EXISTS `bottimers_old`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `bottimers_old` (
  `BotID` int(10) unsigned NOT NULL DEFAULT 0,
  `TimerID` int(10) unsigned NOT NULL DEFAULT 0,
  `Value` int(10) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`BotID`),
  CONSTRAINT `FK_bottimers_1` FOREIGN KEY (`BotID`) REFERENCES `bots_old` (`BotID`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `bug_reports`
--

DROP TABLE IF EXISTS `bug_reports`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `bug_reports` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `zone` varchar(32) NOT NULL DEFAULT 'Unknown',
  `client_version_id` int(11) unsigned NOT NULL DEFAULT 0,
  `client_version_name` varchar(24) NOT NULL DEFAULT 'Unknown',
  `account_id` int(11) unsigned NOT NULL DEFAULT 0,
  `character_id` int(11) unsigned NOT NULL DEFAULT 0,
  `character_name` varchar(64) NOT NULL DEFAULT 'Unknown',
  `reporter_spoof` tinyint(1) NOT NULL DEFAULT 1,
  `category_id` int(11) unsigned NOT NULL DEFAULT 0,
  `category_name` varchar(64) NOT NULL DEFAULT 'Other',
  `reporter_name` varchar(64) NOT NULL DEFAULT 'Unknown',
  `ui_path` varchar(128) NOT NULL DEFAULT 'Unknown',
  `pos_x` float NOT NULL DEFAULT 0,
  `pos_y` float NOT NULL DEFAULT 0,
  `pos_z` float NOT NULL DEFAULT 0,
  `heading` int(11) unsigned NOT NULL DEFAULT 0,
  `time_played` int(11) unsigned NOT NULL DEFAULT 0,
  `target_id` int(11) unsigned NOT NULL DEFAULT 0,
  `target_name` varchar(64) NOT NULL DEFAULT 'Unknown',
  `optional_info_mask` int(11) unsigned NOT NULL DEFAULT 0,
  `_can_duplicate` tinyint(1) NOT NULL DEFAULT 0,
  `_crash_bug` tinyint(1) NOT NULL DEFAULT 0,
  `_target_info` tinyint(1) NOT NULL DEFAULT 0,
  `_character_flags` tinyint(1) NOT NULL DEFAULT 0,
  `_unknown_value` tinyint(1) NOT NULL DEFAULT 0,
  `bug_report` varchar(1024) NOT NULL DEFAULT '',
  `system_info` varchar(1024) NOT NULL DEFAULT '',
  `report_datetime` datetime NOT NULL DEFAULT current_timestamp(),
  `bug_status` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `last_review` datetime NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  `last_reviewer` varchar(64) NOT NULL DEFAULT 'None',
  `reviewer_notes` varchar(1024) NOT NULL DEFAULT '',
  PRIMARY KEY (`id`),
  UNIQUE KEY `id` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `bugs`
--

DROP TABLE IF EXISTS `bugs`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `bugs` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `zone` varchar(32) NOT NULL DEFAULT '',
  `name` varchar(64) NOT NULL DEFAULT '',
  `ui` varchar(128) NOT NULL DEFAULT '',
  `x` float NOT NULL DEFAULT 0,
  `y` float NOT NULL DEFAULT 0,
  `z` float NOT NULL DEFAULT 0,
  `type` varchar(64) NOT NULL DEFAULT '',
  `flag` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `target` varchar(64) DEFAULT NULL,
  `bug` text NOT NULL,
  `date` date NOT NULL DEFAULT '0000-00-00',
  `status` tinyint(3) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `id` (`id`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `buyer`
--

DROP TABLE IF EXISTS `buyer`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `buyer` (
  `charid` int(11) NOT NULL DEFAULT 0,
  `buyslot` int(11) NOT NULL DEFAULT 0,
  `itemid` int(11) NOT NULL DEFAULT 0,
  `itemname` varchar(65) NOT NULL DEFAULT '',
  `quantity` int(11) NOT NULL DEFAULT 0,
  `price` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`charid`,`buyslot`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `char_create_combinations`
--

DROP TABLE IF EXISTS `char_create_combinations`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `char_create_combinations` (
  `allocation_id` int(10) unsigned NOT NULL,
  `race` int(10) unsigned NOT NULL,
  `class` int(10) unsigned NOT NULL,
  `deity` int(10) unsigned NOT NULL,
  `start_zone` int(10) unsigned NOT NULL,
  `expansions_req` int(10) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`race`,`class`,`deity`,`start_zone`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `char_create_point_allocations`
--

DROP TABLE IF EXISTS `char_create_point_allocations`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `char_create_point_allocations` (
  `id` int(10) unsigned NOT NULL,
  `base_str` int(10) unsigned NOT NULL,
  `base_sta` int(10) unsigned NOT NULL,
  `base_dex` int(10) unsigned NOT NULL,
  `base_agi` int(10) unsigned NOT NULL,
  `base_int` int(10) unsigned NOT NULL,
  `base_wis` int(10) unsigned NOT NULL,
  `base_cha` int(10) unsigned NOT NULL,
  `alloc_str` int(10) unsigned NOT NULL,
  `alloc_sta` int(10) unsigned NOT NULL,
  `alloc_dex` int(10) unsigned NOT NULL,
  `alloc_agi` int(10) unsigned NOT NULL,
  `alloc_int` int(10) unsigned NOT NULL,
  `alloc_wis` int(10) unsigned NOT NULL,
  `alloc_cha` int(10) unsigned NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `char_recipe_list`
--

DROP TABLE IF EXISTS `char_recipe_list`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `char_recipe_list` (
  `char_id` int(11) NOT NULL,
  `recipe_id` int(11) NOT NULL,
  `madecount` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`char_id`,`recipe_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_activities`
--

DROP TABLE IF EXISTS `character_activities`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_activities` (
  `charid` int(11) unsigned NOT NULL DEFAULT 0,
  `taskid` int(11) unsigned NOT NULL DEFAULT 0,
  `activityid` int(11) unsigned NOT NULL DEFAULT 0,
  `donecount` int(11) unsigned NOT NULL DEFAULT 0,
  `completed` tinyint(1) DEFAULT 0,
  PRIMARY KEY (`charid`,`taskid`,`activityid`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_alt_currency`
--

DROP TABLE IF EXISTS `character_alt_currency`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_alt_currency` (
  `char_id` int(10) unsigned NOT NULL,
  `currency_id` int(10) unsigned NOT NULL,
  `amount` int(10) unsigned NOT NULL,
  PRIMARY KEY (`char_id`,`currency_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_alternate_abilities`
--

DROP TABLE IF EXISTS `character_alternate_abilities`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_alternate_abilities` (
  `id` int(11) unsigned NOT NULL DEFAULT 0,
  `aa_id` smallint(11) unsigned NOT NULL DEFAULT 0,
  `aa_value` smallint(11) unsigned NOT NULL DEFAULT 0,
  `charges` smallint(11) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`,`aa_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_alternate_abilities_old`
--

DROP TABLE IF EXISTS `character_alternate_abilities_old`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_alternate_abilities_old` (
  `id` int(11) unsigned NOT NULL DEFAULT 0,
  `slot` smallint(11) unsigned NOT NULL DEFAULT 0,
  `aa_id` smallint(11) unsigned NOT NULL DEFAULT 0,
  `aa_value` smallint(11) unsigned NOT NULL DEFAULT 0,
  `charges` smallint(11) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`,`slot`),
  KEY `id` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_auras`
--

DROP TABLE IF EXISTS `character_auras`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_auras` (
  `id` int(10) NOT NULL,
  `slot` tinyint(10) NOT NULL,
  `spell_id` int(10) NOT NULL,
  PRIMARY KEY (`id`,`slot`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_backup`
--

DROP TABLE IF EXISTS `character_backup`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_backup` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `backupreason` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `charid` int(10) unsigned NOT NULL DEFAULT 0,
  `account_id` int(10) unsigned NOT NULL DEFAULT 0,
  `name` varchar(64) NOT NULL DEFAULT '',
  `profile` blob DEFAULT NULL,
  `x` float NOT NULL DEFAULT 0,
  `y` float NOT NULL DEFAULT 0,
  `z` float NOT NULL DEFAULT 0,
  `zoneid` smallint(5) NOT NULL DEFAULT 0,
  `alt_adv` blob DEFAULT NULL,
  `ts` timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  `class` tinyint(4) NOT NULL DEFAULT 0,
  `level` mediumint(8) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `name` (`name`),
  KEY `charid` (`charid`)
) ENGINE=MyISAM AUTO_INCREMENT=728 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_bandolier`
--

DROP TABLE IF EXISTS `character_bandolier`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_bandolier` (
  `id` int(11) unsigned NOT NULL DEFAULT 0,
  `bandolier_id` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `bandolier_slot` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `item_id` int(11) unsigned NOT NULL DEFAULT 0,
  `icon` int(11) unsigned NOT NULL DEFAULT 0,
  `bandolier_name` varchar(32) NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`,`bandolier_id`,`bandolier_slot`),
  KEY `id` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_bind`
--

DROP TABLE IF EXISTS `character_bind`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_bind` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `slot` int(4) NOT NULL DEFAULT 0,
  `zone_id` smallint(11) unsigned NOT NULL DEFAULT 0,
  `instance_id` mediumint(11) unsigned NOT NULL DEFAULT 0,
  `x` float NOT NULL DEFAULT 0,
  `y` float NOT NULL DEFAULT 0,
  `z` float NOT NULL DEFAULT 0,
  `heading` float NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`,`slot`),
  KEY `id` (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=279 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_buffs`
--

DROP TABLE IF EXISTS `character_buffs`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_buffs` (
  `character_id` int(10) unsigned NOT NULL,
  `slot_id` tinyint(3) unsigned NOT NULL,
  `spell_id` smallint(10) unsigned NOT NULL,
  `caster_level` tinyint(3) unsigned NOT NULL,
  `caster_name` varchar(64) NOT NULL,
  `ticsremaining` int(11) NOT NULL,
  `counters` int(10) unsigned NOT NULL,
  `numhits` int(10) unsigned NOT NULL,
  `melee_rune` int(10) unsigned NOT NULL,
  `magic_rune` int(10) unsigned NOT NULL,
  `persistent` tinyint(3) unsigned NOT NULL,
  `dot_rune` int(10) NOT NULL DEFAULT 0,
  `caston_x` int(10) NOT NULL DEFAULT 0,
  `caston_y` int(10) NOT NULL DEFAULT 0,
  `caston_z` int(10) NOT NULL DEFAULT 0,
  `ExtraDIChance` int(10) NOT NULL DEFAULT 0,
  `instrument_mod` int(10) NOT NULL DEFAULT 10,
  PRIMARY KEY (`character_id`,`slot_id`),
  KEY `character_id` (`character_id`)
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_corpse_items`
--

DROP TABLE IF EXISTS `character_corpse_items`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_corpse_items` (
  `corpse_id` int(11) unsigned NOT NULL,
  `equip_slot` int(11) unsigned NOT NULL,
  `item_id` int(11) unsigned DEFAULT NULL,
  `charges` int(11) unsigned DEFAULT NULL,
  `aug_1` int(11) unsigned DEFAULT 0,
  `aug_2` int(11) unsigned DEFAULT 0,
  `aug_3` int(11) unsigned DEFAULT 0,
  `aug_4` int(11) unsigned DEFAULT 0,
  `aug_5` int(11) unsigned DEFAULT 0,
  `aug_6` int(11) unsigned DEFAULT 0,
  `attuned` smallint(5) NOT NULL DEFAULT 0,
  `custom_data` text DEFAULT NULL,
  `ornamenticon` int(10) unsigned NOT NULL DEFAULT 0,
  `ornamentidfile` int(10) unsigned NOT NULL DEFAULT 0,
  `ornament_hero_model` int(10) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`corpse_id`,`equip_slot`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_corpses`
--

DROP TABLE IF EXISTS `character_corpses`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_corpses` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `charid` int(10) unsigned NOT NULL DEFAULT 0,
  `charname` varchar(64) NOT NULL DEFAULT '',
  `zone_id` smallint(5) NOT NULL DEFAULT 0,
  `instance_id` smallint(5) unsigned NOT NULL DEFAULT 0,
  `x` float NOT NULL DEFAULT 0,
  `y` float NOT NULL DEFAULT 0,
  `z` float NOT NULL DEFAULT 0,
  `heading` float NOT NULL DEFAULT 0,
  `time_of_death` datetime NOT NULL DEFAULT current_timestamp(),
  `guild_consent_id` int(11) unsigned NOT NULL DEFAULT 0,
  `is_rezzed` tinyint(3) unsigned DEFAULT 0,
  `is_buried` tinyint(3) NOT NULL DEFAULT 0,
  `was_at_graveyard` tinyint(3) NOT NULL DEFAULT 0,
  `is_locked` tinyint(11) DEFAULT 0,
  `exp` int(11) unsigned DEFAULT 0,
  `size` int(11) unsigned DEFAULT 0,
  `level` int(11) unsigned DEFAULT 0,
  `race` int(11) unsigned DEFAULT 0,
  `gender` int(11) unsigned DEFAULT 0,
  `class` int(11) unsigned DEFAULT 0,
  `deity` int(11) unsigned DEFAULT 0,
  `texture` int(11) unsigned DEFAULT 0,
  `helm_texture` int(11) unsigned DEFAULT 0,
  `copper` int(11) unsigned DEFAULT 0,
  `silver` int(11) unsigned DEFAULT 0,
  `gold` int(11) unsigned DEFAULT 0,
  `platinum` int(11) unsigned DEFAULT 0,
  `hair_color` int(11) unsigned DEFAULT 0,
  `beard_color` int(11) unsigned DEFAULT 0,
  `eye_color_1` int(11) unsigned DEFAULT 0,
  `eye_color_2` int(11) unsigned DEFAULT 0,
  `hair_style` int(11) unsigned DEFAULT 0,
  `face` int(11) unsigned DEFAULT 0,
  `beard` int(11) unsigned DEFAULT 0,
  `drakkin_heritage` int(11) unsigned DEFAULT 0,
  `drakkin_tattoo` int(11) unsigned DEFAULT 0,
  `drakkin_details` int(11) unsigned DEFAULT 0,
  `wc_1` int(11) unsigned DEFAULT 0,
  `wc_2` int(11) unsigned DEFAULT 0,
  `wc_3` int(11) unsigned DEFAULT 0,
  `wc_4` int(11) unsigned DEFAULT 0,
  `wc_5` int(11) unsigned DEFAULT 0,
  `wc_6` int(11) unsigned DEFAULT 0,
  `wc_7` int(11) unsigned DEFAULT 0,
  `wc_8` int(11) unsigned DEFAULT 0,
  `wc_9` int(11) unsigned DEFAULT 0,
  `rez_time` int(11) unsigned NOT NULL DEFAULT 0,
  `gm_exp` int(11) unsigned NOT NULL DEFAULT 0,
  `killed_by` int(11) unsigned NOT NULL DEFAULT 0,
  `rezzable` tinyint(1) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `zoneid` (`zone_id`),
  KEY `instanceid` (`instance_id`)
) ENGINE=MyISAM AUTO_INCREMENT=366 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_currency`
--

DROP TABLE IF EXISTS `character_currency`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_currency` (
  `id` int(11) unsigned NOT NULL DEFAULT 0,
  `platinum` int(11) unsigned NOT NULL DEFAULT 0,
  `gold` int(11) unsigned NOT NULL DEFAULT 0,
  `silver` int(11) unsigned NOT NULL DEFAULT 0,
  `copper` int(11) unsigned NOT NULL DEFAULT 0,
  `platinum_bank` int(11) unsigned NOT NULL DEFAULT 0,
  `gold_bank` int(11) unsigned NOT NULL DEFAULT 0,
  `silver_bank` int(11) unsigned NOT NULL DEFAULT 0,
  `copper_bank` int(11) unsigned NOT NULL DEFAULT 0,
  `platinum_cursor` int(11) unsigned NOT NULL DEFAULT 0,
  `gold_cursor` int(11) unsigned NOT NULL DEFAULT 0,
  `silver_cursor` int(11) unsigned NOT NULL DEFAULT 0,
  `copper_cursor` int(11) unsigned NOT NULL DEFAULT 0,
  `radiant_crystals` int(11) unsigned NOT NULL DEFAULT 0,
  `career_radiant_crystals` int(11) unsigned NOT NULL DEFAULT 0,
  `ebon_crystals` int(11) unsigned NOT NULL DEFAULT 0,
  `career_ebon_crystals` int(11) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `id` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_data`
--

DROP TABLE IF EXISTS `character_data`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_data` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `account_id` int(11) NOT NULL DEFAULT 0,
  `name` varchar(64) CHARACTER SET utf8mb3 COLLATE utf8mb3_unicode_ci NOT NULL DEFAULT '',
  `last_name` varchar(64) CHARACTER SET utf8mb3 COLLATE utf8mb3_unicode_ci NOT NULL DEFAULT '',
  `title` varchar(32) CHARACTER SET utf8mb3 COLLATE utf8mb3_unicode_ci NOT NULL DEFAULT '',
  `suffix` varchar(32) CHARACTER SET utf8mb3 COLLATE utf8mb3_unicode_ci NOT NULL DEFAULT '',
  `zone_id` int(11) unsigned NOT NULL DEFAULT 0,
  `zone_instance` int(11) unsigned NOT NULL DEFAULT 0,
  `y` float NOT NULL DEFAULT 0,
  `x` float NOT NULL DEFAULT 0,
  `z` float NOT NULL DEFAULT 0,
  `heading` float NOT NULL DEFAULT 0,
  `gender` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `race` smallint(11) unsigned NOT NULL DEFAULT 0,
  `class` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `level` int(11) unsigned NOT NULL DEFAULT 0,
  `deity` int(11) unsigned NOT NULL DEFAULT 0,
  `birthday` int(11) unsigned NOT NULL DEFAULT 0,
  `last_login` int(11) unsigned NOT NULL DEFAULT 0,
  `time_played` int(11) unsigned NOT NULL DEFAULT 0,
  `level2` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `anon` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `gm` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `face` int(11) unsigned NOT NULL DEFAULT 0,
  `hair_color` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `hair_style` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `beard` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `beard_color` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `eye_color_1` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `eye_color_2` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `drakkin_heritage` int(11) unsigned NOT NULL DEFAULT 0,
  `drakkin_tattoo` int(11) unsigned NOT NULL DEFAULT 0,
  `drakkin_details` int(11) unsigned NOT NULL DEFAULT 0,
  `ability_time_seconds` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `ability_number` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `ability_time_minutes` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `ability_time_hours` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `exp` int(11) unsigned NOT NULL DEFAULT 0,
  `exp_enabled` tinyint(1) unsigned NOT NULL DEFAULT 1,
  `aa_points_spent` int(11) unsigned NOT NULL DEFAULT 0,
  `aa_exp` int(11) unsigned NOT NULL DEFAULT 0,
  `aa_points` int(11) unsigned NOT NULL DEFAULT 0,
  `group_leadership_exp` int(11) unsigned NOT NULL DEFAULT 0,
  `raid_leadership_exp` int(11) unsigned NOT NULL DEFAULT 0,
  `group_leadership_points` int(11) unsigned NOT NULL DEFAULT 0,
  `raid_leadership_points` int(11) unsigned NOT NULL DEFAULT 0,
  `points` int(11) unsigned NOT NULL DEFAULT 0,
  `cur_hp` int(11) unsigned NOT NULL DEFAULT 0,
  `mana` int(11) unsigned NOT NULL DEFAULT 0,
  `endurance` int(11) unsigned NOT NULL DEFAULT 0,
  `intoxication` int(11) unsigned NOT NULL DEFAULT 0,
  `str` int(11) unsigned NOT NULL DEFAULT 0,
  `sta` int(11) unsigned NOT NULL DEFAULT 0,
  `cha` int(11) unsigned NOT NULL DEFAULT 0,
  `dex` int(11) unsigned NOT NULL DEFAULT 0,
  `int` int(11) unsigned NOT NULL DEFAULT 0,
  `agi` int(11) unsigned NOT NULL DEFAULT 0,
  `wis` int(11) unsigned NOT NULL DEFAULT 0,
  `extra_haste` int(11) NOT NULL DEFAULT 0,
  `zone_change_count` int(11) unsigned NOT NULL DEFAULT 0,
  `toxicity` int(11) unsigned NOT NULL DEFAULT 0,
  `hunger_level` int(11) unsigned NOT NULL DEFAULT 0,
  `thirst_level` int(11) unsigned NOT NULL DEFAULT 0,
  `ability_up` int(11) unsigned NOT NULL DEFAULT 0,
  `ldon_points_guk` int(11) unsigned NOT NULL DEFAULT 0,
  `ldon_points_mir` int(11) unsigned NOT NULL DEFAULT 0,
  `ldon_points_mmc` int(11) unsigned NOT NULL DEFAULT 0,
  `ldon_points_ruj` int(11) unsigned NOT NULL DEFAULT 0,
  `ldon_points_tak` int(11) unsigned NOT NULL DEFAULT 0,
  `ldon_points_available` int(11) unsigned NOT NULL DEFAULT 0,
  `tribute_time_remaining` int(11) unsigned NOT NULL DEFAULT 0,
  `career_tribute_points` int(11) unsigned NOT NULL DEFAULT 0,
  `tribute_points` int(11) unsigned NOT NULL DEFAULT 0,
  `tribute_active` int(11) unsigned NOT NULL DEFAULT 0,
  `pvp_status` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `pvp_kills` int(11) unsigned NOT NULL DEFAULT 0,
  `pvp_deaths` int(11) unsigned NOT NULL DEFAULT 0,
  `pvp_current_points` int(11) unsigned NOT NULL DEFAULT 0,
  `pvp_career_points` int(11) unsigned NOT NULL DEFAULT 0,
  `pvp_best_kill_streak` int(11) unsigned NOT NULL DEFAULT 0,
  `pvp_worst_death_streak` int(11) unsigned NOT NULL DEFAULT 0,
  `pvp_current_kill_streak` int(11) unsigned NOT NULL DEFAULT 0,
  `pvp2` int(11) unsigned NOT NULL DEFAULT 0,
  `pvp_type` int(11) unsigned NOT NULL DEFAULT 0,
  `show_helm` int(11) unsigned NOT NULL DEFAULT 0,
  `group_auto_consent` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `raid_auto_consent` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `guild_auto_consent` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `leadership_exp_on` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `RestTimer` int(11) unsigned NOT NULL DEFAULT 0,
  `air_remaining` int(11) unsigned NOT NULL DEFAULT 0,
  `autosplit_enabled` int(11) unsigned NOT NULL DEFAULT 0,
  `lfp` tinyint(1) unsigned NOT NULL DEFAULT 0,
  `lfg` tinyint(1) unsigned NOT NULL DEFAULT 0,
  `mailkey` char(16) CHARACTER SET utf8mb3 COLLATE utf8mb3_unicode_ci NOT NULL DEFAULT '',
  `xtargets` tinyint(3) unsigned NOT NULL DEFAULT 5,
  `firstlogon` tinyint(3) NOT NULL DEFAULT 0,
  `e_aa_effects` int(11) unsigned NOT NULL DEFAULT 0,
  `e_percent_to_aa` int(11) unsigned NOT NULL DEFAULT 0,
  `e_expended_aa_spent` int(11) unsigned NOT NULL DEFAULT 0,
  `aa_points_spent_old` int(11) unsigned NOT NULL DEFAULT 0,
  `aa_points_old` int(11) unsigned NOT NULL DEFAULT 0,
  `e_last_invsnapshot` int(11) unsigned NOT NULL DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `name` (`name`),
  KEY `account_id` (`account_id`)
) ENGINE=InnoDB AUTO_INCREMENT=279 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_data__`
--

DROP TABLE IF EXISTS `character_data__`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_data__` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `account_id` int(11) NOT NULL DEFAULT 0,
  `name` varchar(64) NOT NULL DEFAULT '',
  `last_name` varchar(64) NOT NULL DEFAULT '',
  `title` varchar(32) NOT NULL DEFAULT '',
  `suffix` varchar(32) NOT NULL DEFAULT '',
  `zone_id` int(11) unsigned NOT NULL DEFAULT 0,
  `zone_instance` int(11) unsigned NOT NULL DEFAULT 0,
  `y` float NOT NULL DEFAULT 0,
  `x` float NOT NULL DEFAULT 0,
  `z` float NOT NULL DEFAULT 0,
  `heading` float NOT NULL DEFAULT 0,
  `gender` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `race` smallint(11) unsigned NOT NULL DEFAULT 0,
  `class` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `level` int(11) unsigned NOT NULL DEFAULT 0,
  `deity` int(11) unsigned NOT NULL DEFAULT 0,
  `birthday` int(11) unsigned NOT NULL DEFAULT 0,
  `last_login` int(11) unsigned NOT NULL DEFAULT 0,
  `time_played` int(11) unsigned NOT NULL DEFAULT 0,
  `level2` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `anon` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `gm` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `face` int(11) unsigned NOT NULL DEFAULT 0,
  `hair_color` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `hair_style` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `beard` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `beard_color` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `eye_color_1` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `eye_color_2` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `drakkin_heritage` int(11) unsigned NOT NULL DEFAULT 0,
  `drakkin_tattoo` int(11) unsigned NOT NULL DEFAULT 0,
  `drakkin_details` int(11) unsigned NOT NULL DEFAULT 0,
  `ability_time_seconds` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `ability_number` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `ability_time_minutes` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `ability_time_hours` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `exp` int(11) unsigned NOT NULL DEFAULT 0,
  `aa_points_spent` int(11) unsigned NOT NULL DEFAULT 0,
  `aa_exp` int(11) unsigned NOT NULL DEFAULT 0,
  `aa_points` int(11) unsigned NOT NULL DEFAULT 0,
  `group_leadership_exp` int(11) unsigned NOT NULL DEFAULT 0,
  `raid_leadership_exp` int(11) unsigned NOT NULL DEFAULT 0,
  `group_leadership_points` int(11) unsigned NOT NULL DEFAULT 0,
  `raid_leadership_points` int(11) unsigned NOT NULL DEFAULT 0,
  `points` int(11) unsigned NOT NULL DEFAULT 0,
  `cur_hp` int(11) unsigned NOT NULL DEFAULT 0,
  `mana` int(11) unsigned NOT NULL DEFAULT 0,
  `endurance` int(11) unsigned NOT NULL DEFAULT 0,
  `intoxication` int(11) unsigned NOT NULL DEFAULT 0,
  `str` int(11) unsigned NOT NULL DEFAULT 0,
  `sta` int(11) unsigned NOT NULL DEFAULT 0,
  `cha` int(11) unsigned NOT NULL DEFAULT 0,
  `dex` int(11) unsigned NOT NULL DEFAULT 0,
  `int` int(11) unsigned NOT NULL DEFAULT 0,
  `agi` int(11) unsigned NOT NULL DEFAULT 0,
  `wis` int(11) unsigned NOT NULL DEFAULT 0,
  `zone_change_count` int(11) unsigned NOT NULL DEFAULT 0,
  `toxicity` int(11) unsigned NOT NULL DEFAULT 0,
  `hunger_level` int(11) unsigned NOT NULL DEFAULT 0,
  `thirst_level` int(11) unsigned NOT NULL DEFAULT 0,
  `ability_up` int(11) unsigned NOT NULL DEFAULT 0,
  `ldon_points_guk` int(11) unsigned NOT NULL DEFAULT 0,
  `ldon_points_mir` int(11) unsigned NOT NULL DEFAULT 0,
  `ldon_points_mmc` int(11) unsigned NOT NULL DEFAULT 0,
  `ldon_points_ruj` int(11) unsigned NOT NULL DEFAULT 0,
  `ldon_points_tak` int(11) unsigned NOT NULL DEFAULT 0,
  `ldon_points_available` int(11) unsigned NOT NULL DEFAULT 0,
  `tribute_time_remaining` int(11) unsigned NOT NULL DEFAULT 0,
  `career_tribute_points` int(11) unsigned NOT NULL DEFAULT 0,
  `tribute_points` int(11) unsigned NOT NULL DEFAULT 0,
  `tribute_active` int(11) unsigned NOT NULL DEFAULT 0,
  `pvp_status` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `pvp_kills` int(11) unsigned NOT NULL DEFAULT 0,
  `pvp_deaths` int(11) unsigned NOT NULL DEFAULT 0,
  `pvp_current_points` int(11) unsigned NOT NULL DEFAULT 0,
  `pvp_career_points` int(11) unsigned NOT NULL DEFAULT 0,
  `pvp_best_kill_streak` int(11) unsigned NOT NULL DEFAULT 0,
  `pvp_worst_death_streak` int(11) unsigned NOT NULL DEFAULT 0,
  `pvp_current_kill_streak` int(11) unsigned NOT NULL DEFAULT 0,
  `pvp2` int(11) unsigned NOT NULL DEFAULT 0,
  `pvp_type` int(11) unsigned NOT NULL DEFAULT 0,
  `show_helm` int(11) unsigned NOT NULL DEFAULT 0,
  `group_auto_consent` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `raid_auto_consent` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `guild_auto_consent` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `leadership_exp_on` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `RestTimer` int(11) unsigned NOT NULL DEFAULT 0,
  `air_remaining` int(11) unsigned NOT NULL DEFAULT 0,
  `autosplit_enabled` int(11) unsigned NOT NULL DEFAULT 0,
  `lfp` tinyint(1) unsigned NOT NULL DEFAULT 0,
  `lfg` tinyint(1) unsigned NOT NULL DEFAULT 0,
  `mailkey` char(16) NOT NULL DEFAULT '',
  `xtargets` tinyint(3) unsigned NOT NULL DEFAULT 5,
  `firstlogon` tinyint(3) NOT NULL DEFAULT 0,
  `e_aa_effects` int(11) unsigned NOT NULL DEFAULT 0,
  `e_percent_to_aa` int(11) unsigned NOT NULL DEFAULT 0,
  `e_expended_aa_spent` int(11) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `name` (`name`),
  KEY `account_id` (`account_id`)
) ENGINE=InnoDB AUTO_INCREMENT=41 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_disciplines`
--

DROP TABLE IF EXISTS `character_disciplines`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_disciplines` (
  `id` int(11) unsigned NOT NULL DEFAULT 0,
  `slot_id` smallint(11) unsigned NOT NULL DEFAULT 0,
  `disc_id` smallint(11) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`,`slot_id`),
  KEY `id` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_enabledtasks`
--

DROP TABLE IF EXISTS `character_enabledtasks`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_enabledtasks` (
  `charid` int(11) unsigned NOT NULL DEFAULT 0,
  `taskid` int(11) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`charid`,`taskid`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_exp_modifiers`
--

DROP TABLE IF EXISTS `character_exp_modifiers`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_exp_modifiers` (
  `character_id` int(11) NOT NULL,
  `zone_id` int(11) NOT NULL,
  `instance_version` int(11) NOT NULL DEFAULT -1,
  `aa_modifier` float NOT NULL,
  `exp_modifier` float NOT NULL,
  PRIMARY KEY (`character_id`,`zone_id`,`instance_version`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci ROW_FORMAT=COMPACT;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_expedition_lockouts`
--

DROP TABLE IF EXISTS `character_expedition_lockouts`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_expedition_lockouts` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `character_id` int(10) unsigned NOT NULL,
  `expedition_name` varchar(128) NOT NULL,
  `event_name` varchar(256) NOT NULL,
  `expire_time` datetime NOT NULL DEFAULT current_timestamp(),
  `duration` int(10) unsigned NOT NULL DEFAULT 0,
  `from_expedition_uuid` varchar(36) NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `character_id_expedition_name_event_name` (`character_id`,`expedition_name`,`event_name`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_inspect_messages`
--

DROP TABLE IF EXISTS `character_inspect_messages`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_inspect_messages` (
  `id` int(11) unsigned NOT NULL DEFAULT 0,
  `inspect_message` varchar(255) NOT NULL DEFAULT '',
  PRIMARY KEY (`id`),
  KEY `id` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_instance_safereturns`
--

DROP TABLE IF EXISTS `character_instance_safereturns`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_instance_safereturns` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `character_id` int(10) unsigned NOT NULL,
  `instance_zone_id` int(11) NOT NULL DEFAULT 0,
  `instance_id` int(11) NOT NULL DEFAULT 0,
  `safe_zone_id` int(11) NOT NULL DEFAULT 0,
  `safe_x` float NOT NULL DEFAULT 0,
  `safe_y` float NOT NULL DEFAULT 0,
  `safe_z` float NOT NULL DEFAULT 0,
  `safe_heading` float NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `character_id` (`character_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_item_recast`
--

DROP TABLE IF EXISTS `character_item_recast`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_item_recast` (
  `id` int(11) unsigned NOT NULL DEFAULT 0,
  `recast_type` int(11) unsigned NOT NULL DEFAULT 0,
  `timestamp` int(11) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`,`recast_type`),
  KEY `id` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_languages`
--

DROP TABLE IF EXISTS `character_languages`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_languages` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `lang_id` smallint(11) unsigned NOT NULL DEFAULT 0,
  `value` smallint(11) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`,`lang_id`),
  KEY `id` (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=279 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_leadership_abilities`
--

DROP TABLE IF EXISTS `character_leadership_abilities`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_leadership_abilities` (
  `id` int(11) unsigned NOT NULL DEFAULT 0,
  `slot` smallint(11) unsigned NOT NULL DEFAULT 0,
  `rank` smallint(11) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`,`slot`),
  KEY `id` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_material`
--

DROP TABLE IF EXISTS `character_material`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_material` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `slot` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `blue` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `green` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `red` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `use_tint` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `color` int(11) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`,`slot`),
  KEY `id` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_memmed_spells`
--

DROP TABLE IF EXISTS `character_memmed_spells`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_memmed_spells` (
  `id` int(11) unsigned NOT NULL DEFAULT 0,
  `slot_id` smallint(11) unsigned NOT NULL DEFAULT 0,
  `spell_id` smallint(11) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`,`slot_id`),
  KEY `id` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_old`
--

DROP TABLE IF EXISTS `character_old`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_old` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `account_id` int(11) NOT NULL DEFAULT 0,
  `name` varchar(64) NOT NULL DEFAULT '',
  `profile` blob DEFAULT NULL,
  `timelaston` int(10) unsigned DEFAULT 0,
  `x` float NOT NULL DEFAULT 0,
  `y` float NOT NULL DEFAULT 0,
  `z` float NOT NULL DEFAULT 0,
  `zonename` varchar(30) NOT NULL DEFAULT '',
  `alt_adv` blob DEFAULT NULL,
  `zoneid` smallint(6) NOT NULL DEFAULT 0,
  `instanceid` smallint(5) unsigned NOT NULL DEFAULT 0,
  `pktime` int(8) NOT NULL DEFAULT 0,
  `inventory` blob DEFAULT NULL,
  `groupid` int(10) unsigned NOT NULL DEFAULT 0,
  `extprofile` blob DEFAULT NULL,
  `class` tinyint(4) NOT NULL DEFAULT 0,
  `level` mediumint(8) unsigned NOT NULL DEFAULT 0,
  `lfp` tinyint(1) unsigned NOT NULL DEFAULT 0,
  `lfg` tinyint(1) unsigned NOT NULL DEFAULT 0,
  `mailkey` varchar(16) NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  UNIQUE KEY `name` (`name`),
  KEY `account_id` (`account_id`)
) ENGINE=MyISAM AUTO_INCREMENT=36 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_parcels`
--

DROP TABLE IF EXISTS `character_parcels`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_parcels` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `char_id` int(10) unsigned NOT NULL DEFAULT 0,
  `item_id` int(10) unsigned NOT NULL DEFAULT 0,
  `aug_slot_1` int(10) unsigned NOT NULL DEFAULT 0,
  `aug_slot_2` int(10) unsigned NOT NULL DEFAULT 0,
  `aug_slot_3` int(10) unsigned NOT NULL DEFAULT 0,
  `aug_slot_4` int(10) unsigned NOT NULL DEFAULT 0,
  `aug_slot_5` int(10) unsigned NOT NULL DEFAULT 0,
  `aug_slot_6` int(10) unsigned NOT NULL DEFAULT 0,
  `slot_id` int(10) unsigned NOT NULL DEFAULT 0,
  `quantity` int(10) unsigned NOT NULL DEFAULT 0,
  `from_name` varchar(64) DEFAULT NULL,
  `note` varchar(1024) DEFAULT NULL,
  `sent_date` datetime DEFAULT NULL,
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE KEY `data_constraint` (`slot_id`,`char_id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_parcels_containers`
--

DROP TABLE IF EXISTS `character_parcels_containers`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_parcels_containers` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `parcels_id` int(10) unsigned NOT NULL DEFAULT 0,
  `slot_id` int(10) unsigned NOT NULL DEFAULT 0,
  `item_id` int(10) unsigned NOT NULL DEFAULT 0,
  `aug_slot_1` int(10) unsigned NOT NULL DEFAULT 0,
  `aug_slot_2` int(10) unsigned NOT NULL DEFAULT 0,
  `aug_slot_3` int(10) unsigned NOT NULL DEFAULT 0,
  `aug_slot_4` int(10) unsigned NOT NULL DEFAULT 0,
  `aug_slot_5` int(10) unsigned NOT NULL DEFAULT 0,
  `aug_slot_6` int(10) unsigned NOT NULL DEFAULT 0,
  `quantity` int(10) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`) USING BTREE,
  KEY `fk_character_parcels_id` (`parcels_id`) USING BTREE,
  CONSTRAINT `fk_character_parcels_id` FOREIGN KEY (`parcels_id`) REFERENCES `character_parcels` (`id`) ON DELETE CASCADE ON UPDATE NO ACTION
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_peqzone_flags`
--

DROP TABLE IF EXISTS `character_peqzone_flags`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_peqzone_flags` (
  `id` int(11) NOT NULL DEFAULT 0,
  `zone_id` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`,`zone_id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci ROW_FORMAT=COMPACT;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_pet_buffs`
--

DROP TABLE IF EXISTS `character_pet_buffs`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_pet_buffs` (
  `char_id` int(11) NOT NULL,
  `pet` int(11) NOT NULL,
  `slot` int(11) NOT NULL,
  `spell_id` int(11) NOT NULL,
  `caster_level` tinyint(4) NOT NULL DEFAULT 0,
  `castername` varchar(64) NOT NULL DEFAULT '',
  `ticsremaining` int(11) NOT NULL DEFAULT 0,
  `counters` int(11) NOT NULL DEFAULT 0,
  `numhits` int(11) NOT NULL DEFAULT 0,
  `rune` int(11) NOT NULL DEFAULT 0,
  `instrument_mod` tinyint(3) unsigned NOT NULL DEFAULT 10,
  PRIMARY KEY (`char_id`,`pet`,`slot`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_pet_info`
--

DROP TABLE IF EXISTS `character_pet_info`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_pet_info` (
  `char_id` int(11) NOT NULL,
  `pet` int(11) NOT NULL,
  `petname` varchar(64) NOT NULL DEFAULT '',
  `petpower` int(11) NOT NULL DEFAULT 0,
  `spell_id` int(11) NOT NULL DEFAULT 0,
  `hp` int(11) NOT NULL DEFAULT 0,
  `mana` int(11) NOT NULL DEFAULT 0,
  `size` float NOT NULL DEFAULT 0,
  `taunting` tinyint(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (`char_id`,`pet`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_pet_inventory`
--

DROP TABLE IF EXISTS `character_pet_inventory`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_pet_inventory` (
  `char_id` int(11) NOT NULL,
  `pet` int(11) NOT NULL,
  `slot` int(11) NOT NULL,
  `item_id` int(11) NOT NULL,
  PRIMARY KEY (`char_id`,`pet`,`slot`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_potionbelt`
--

DROP TABLE IF EXISTS `character_potionbelt`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_potionbelt` (
  `id` int(11) unsigned NOT NULL DEFAULT 0,
  `potion_id` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `item_id` int(11) unsigned NOT NULL DEFAULT 0,
  `icon` int(11) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`,`potion_id`),
  KEY `id` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_skills`
--

DROP TABLE IF EXISTS `character_skills`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_skills` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `skill_id` smallint(11) unsigned NOT NULL DEFAULT 0,
  `value` smallint(11) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`,`skill_id`),
  KEY `id` (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=279 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_spells`
--

DROP TABLE IF EXISTS `character_spells`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_spells` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `slot_id` smallint(11) unsigned NOT NULL DEFAULT 0,
  `spell_id` smallint(11) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`,`slot_id`),
  KEY `id` (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=279 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_stats_record`
--

DROP TABLE IF EXISTS `character_stats_record`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_stats_record` (
  `character_id` int(11) NOT NULL DEFAULT 0,
  `name` varchar(100) DEFAULT NULL,
  `status` int(11) DEFAULT 0,
  `level` int(11) DEFAULT 0,
  `class` int(11) DEFAULT 0,
  `race` int(11) DEFAULT 0,
  `aa_points` int(11) DEFAULT 0,
  `hp` bigint(20) DEFAULT 0,
  `mana` bigint(20) DEFAULT 0,
  `endurance` bigint(20) DEFAULT 0,
  `ac` int(11) DEFAULT 0,
  `strength` int(11) DEFAULT 0,
  `stamina` int(11) DEFAULT 0,
  `dexterity` int(11) DEFAULT 0,
  `agility` int(11) DEFAULT 0,
  `intelligence` int(11) DEFAULT 0,
  `wisdom` int(11) DEFAULT 0,
  `charisma` int(11) DEFAULT 0,
  `magic_resist` int(11) DEFAULT 0,
  `fire_resist` int(11) DEFAULT 0,
  `cold_resist` int(11) DEFAULT 0,
  `poison_resist` int(11) DEFAULT 0,
  `disease_resist` int(11) DEFAULT 0,
  `corruption_resist` int(11) DEFAULT 0,
  `heroic_strength` int(11) DEFAULT 0,
  `heroic_stamina` int(11) DEFAULT 0,
  `heroic_dexterity` int(11) DEFAULT 0,
  `heroic_agility` int(11) DEFAULT 0,
  `heroic_intelligence` int(11) DEFAULT 0,
  `heroic_wisdom` int(11) DEFAULT 0,
  `heroic_charisma` int(11) DEFAULT 0,
  `heroic_magic_resist` int(11) DEFAULT 0,
  `heroic_fire_resist` int(11) DEFAULT 0,
  `heroic_cold_resist` int(11) DEFAULT 0,
  `heroic_poison_resist` int(11) DEFAULT 0,
  `heroic_disease_resist` int(11) DEFAULT 0,
  `heroic_corruption_resist` int(11) DEFAULT 0,
  `haste` int(11) DEFAULT 0,
  `accuracy` int(11) DEFAULT 0,
  `attack` int(11) DEFAULT 0,
  `avoidance` int(11) DEFAULT 0,
  `clairvoyance` int(11) DEFAULT 0,
  `combat_effects` int(11) DEFAULT 0,
  `damage_shield_mitigation` int(11) DEFAULT 0,
  `damage_shield` int(11) DEFAULT 0,
  `dot_shielding` int(11) DEFAULT 0,
  `hp_regen` int(11) DEFAULT 0,
  `mana_regen` int(11) DEFAULT 0,
  `endurance_regen` int(11) DEFAULT 0,
  `shielding` int(11) DEFAULT 0,
  `spell_damage` int(11) DEFAULT 0,
  `spell_shielding` int(11) DEFAULT 0,
  `strikethrough` int(11) DEFAULT 0,
  `stun_resist` int(11) DEFAULT 0,
  `backstab` int(11) DEFAULT 0,
  `wind` int(11) DEFAULT 0,
  `brass` int(11) DEFAULT 0,
  `string` int(11) DEFAULT 0,
  `percussion` int(11) DEFAULT 0,
  `singing` int(11) DEFAULT 0,
  `baking` int(11) DEFAULT 0,
  `alchemy` int(11) DEFAULT 0,
  `tailoring` int(11) DEFAULT 0,
  `blacksmithing` int(11) DEFAULT 0,
  `fletching` int(11) DEFAULT 0,
  `brewing` int(11) DEFAULT 0,
  `jewelry` int(11) DEFAULT 0,
  `pottery` int(11) DEFAULT 0,
  `research` int(11) DEFAULT 0,
  `alcohol` int(11) DEFAULT 0,
  `fishing` int(11) DEFAULT 0,
  `tinkering` int(11) DEFAULT 0,
  `created_at` datetime DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL,
  PRIMARY KEY (`character_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_task_timers`
--

DROP TABLE IF EXISTS `character_task_timers`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_task_timers` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `character_id` int(10) unsigned NOT NULL DEFAULT 0,
  `task_id` int(10) unsigned NOT NULL DEFAULT 0,
  `timer_type` int(11) NOT NULL DEFAULT 0,
  `timer_group` int(11) NOT NULL DEFAULT 0,
  `expire_time` datetime NOT NULL DEFAULT current_timestamp(),
  PRIMARY KEY (`id`),
  KEY `character_id` (`character_id`),
  KEY `task_id` (`task_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_tasks`
--

DROP TABLE IF EXISTS `character_tasks`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_tasks` (
  `charid` int(11) unsigned NOT NULL DEFAULT 0,
  `taskid` int(11) unsigned NOT NULL DEFAULT 0,
  `slot` int(11) unsigned NOT NULL DEFAULT 0,
  `type` tinyint(4) NOT NULL DEFAULT 0,
  `acceptedtime` int(11) unsigned DEFAULT NULL,
  `was_rewarded` tinyint(4) NOT NULL DEFAULT 0,
  PRIMARY KEY (`charid`,`taskid`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `character_tribute`
--

DROP TABLE IF EXISTS `character_tribute`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `character_tribute` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `character_id` int(11) unsigned NOT NULL DEFAULT 0,
  `tier` tinyint(11) unsigned NOT NULL DEFAULT 0,
  `tribute` int(11) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `id` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `chatchannel_reserved_names`
--

DROP TABLE IF EXISTS `chatchannel_reserved_names`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `chatchannel_reserved_names` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(64) NOT NULL,
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE KEY `name` (`name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `chatchannels`
--

DROP TABLE IF EXISTS `chatchannels`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `chatchannels` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(64) NOT NULL DEFAULT '',
  `owner` varchar(64) NOT NULL DEFAULT '',
  `password` varchar(64) NOT NULL DEFAULT '',
  `minstatus` int(5) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE KEY `name` (`name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `class_skill`
--

DROP TABLE IF EXISTS `class_skill`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `class_skill` (
  `class` smallint(5) unsigned NOT NULL DEFAULT 0,
  `name` varchar(50) NOT NULL DEFAULT 'Enter a class name for quick reference',
  `skill_0` smallint(5) unsigned DEFAULT 66,
  `skill_1` smallint(5) unsigned DEFAULT 66,
  `skill_2` smallint(5) unsigned DEFAULT 66,
  `skill_3` smallint(5) unsigned DEFAULT 66,
  `skill_4` smallint(5) unsigned DEFAULT 66,
  `skill_5` smallint(5) unsigned DEFAULT 66,
  `skill_6` smallint(5) unsigned DEFAULT 66,
  `skill_7` smallint(5) unsigned DEFAULT 66,
  `skill_8` smallint(5) unsigned DEFAULT 66,
  `skill_9` smallint(5) unsigned DEFAULT 66,
  `skill_10` smallint(5) unsigned DEFAULT 66,
  `skill_11` smallint(5) unsigned DEFAULT 66,
  `skill_12` smallint(5) unsigned DEFAULT 66,
  `skill_13` smallint(5) unsigned DEFAULT 66,
  `skill_14` smallint(5) unsigned DEFAULT 66,
  `skill_15` smallint(5) unsigned DEFAULT 66,
  `skill_16` smallint(5) unsigned DEFAULT 66,
  `skill_17` smallint(5) unsigned DEFAULT 66,
  `skill_18` smallint(5) unsigned DEFAULT 66,
  `skill_19` smallint(5) unsigned DEFAULT 66,
  `skill_20` smallint(5) unsigned DEFAULT 66,
  `skill_21` smallint(5) unsigned DEFAULT 66,
  `skill_22` smallint(5) unsigned DEFAULT 66,
  `skill_23` smallint(5) unsigned DEFAULT 66,
  `skill_24` smallint(5) unsigned DEFAULT 66,
  `skill_25` smallint(5) unsigned DEFAULT 66,
  `skill_26` smallint(5) unsigned DEFAULT 66,
  `skill_27` smallint(5) unsigned DEFAULT 66,
  `skill_28` smallint(5) unsigned DEFAULT 66,
  `skill_29` smallint(5) unsigned DEFAULT 66,
  `skill_30` smallint(5) unsigned DEFAULT 66,
  `skill_31` smallint(5) unsigned DEFAULT 66,
  `skill_32` smallint(5) unsigned DEFAULT 66,
  `skill_33` smallint(5) unsigned DEFAULT 66,
  `skill_34` smallint(5) unsigned DEFAULT 66,
  `skill_35` smallint(5) unsigned DEFAULT 66,
  `skill_36` smallint(5) unsigned DEFAULT 66,
  `skill_37` smallint(5) unsigned DEFAULT 66,
  `skill_38` smallint(5) unsigned DEFAULT 66,
  `skill_39` smallint(5) unsigned DEFAULT 66,
  `skill_40` smallint(5) unsigned DEFAULT 66,
  `skill_41` smallint(5) unsigned DEFAULT 66,
  `skill_42` smallint(5) unsigned DEFAULT 66,
  `skill_43` smallint(5) unsigned DEFAULT 66,
  `skill_44` smallint(5) unsigned DEFAULT 66,
  `skill_45` smallint(5) unsigned DEFAULT 66,
  `skill_46` smallint(5) unsigned DEFAULT 66,
  `skill_47` smallint(5) unsigned DEFAULT 66,
  `skill_48` smallint(5) unsigned DEFAULT 66,
  `skill_49` smallint(5) unsigned DEFAULT 66,
  `skill_50` smallint(5) unsigned DEFAULT 66,
  `skill_51` smallint(5) unsigned DEFAULT 66,
  `skill_52` smallint(5) unsigned DEFAULT 66,
  `skill_53` smallint(5) unsigned DEFAULT 66,
  `skill_54` smallint(5) unsigned DEFAULT 66,
  `skill_55` smallint(5) unsigned DEFAULT 66,
  `skill_56` smallint(5) unsigned DEFAULT 66,
  `skill_57` smallint(5) unsigned DEFAULT 66,
  `skill_58` smallint(5) unsigned DEFAULT 66,
  `skill_59` smallint(5) unsigned DEFAULT 66,
  `skill_60` smallint(5) unsigned DEFAULT 66,
  `skill_61` smallint(5) unsigned DEFAULT 66,
  `skill_62` smallint(5) unsigned DEFAULT 66,
  `skill_63` smallint(5) unsigned DEFAULT 66,
  `skill_64` smallint(5) unsigned DEFAULT 66,
  `skill_65` smallint(5) unsigned DEFAULT 66,
  `skill_66` smallint(5) unsigned DEFAULT 66,
  `skill_67` smallint(5) unsigned DEFAULT 66,
  `skill_68` smallint(5) unsigned DEFAULT 66,
  `skill_69` smallint(5) unsigned DEFAULT 66,
  `skill_70` smallint(5) unsigned DEFAULT 66,
  `skill_71` smallint(5) unsigned DEFAULT 66,
  `skill_72` smallint(5) unsigned DEFAULT 66,
  `skill_73` smallint(5) unsigned DEFAULT 66,
  PRIMARY KEY (`class`),
  KEY `class` (`class`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `client_faction_associations`
--

DROP TABLE IF EXISTS `client_faction_associations`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `client_faction_associations` (
  `faction_id` int(11) NOT NULL,
  `other_faction_id` int(11) NOT NULL,
  `mod` int(11) DEFAULT NULL,
  PRIMARY KEY (`faction_id`,`other_faction_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `client_faction_names`
--

DROP TABLE IF EXISTS `client_faction_names`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `client_faction_names` (
  `id` int(11) NOT NULL,
  `name` varchar(45) NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `client_server_faction_map`
--

DROP TABLE IF EXISTS `client_server_faction_map`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `client_server_faction_map` (
  `clientid` int(11) NOT NULL,
  `serverid` int(11) NOT NULL,
  PRIMARY KEY (`clientid`,`serverid`),
  KEY `serverid` (`serverid`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `command_settings`
--

DROP TABLE IF EXISTS `command_settings`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `command_settings` (
  `command` varchar(128) NOT NULL DEFAULT '',
  `access` int(11) NOT NULL DEFAULT 0,
  `aliases` varchar(256) NOT NULL DEFAULT '',
  PRIMARY KEY (`command`),
  UNIQUE KEY `UK_command_settings_1` (`command`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `command_subsettings`
--

DROP TABLE IF EXISTS `command_subsettings`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `command_subsettings` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `parent_command` varchar(32) NOT NULL,
  `sub_command` varchar(32) NOT NULL,
  `access_level` int(11) unsigned NOT NULL DEFAULT 0,
  `top_level_aliases` varchar(255) NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `command` (`parent_command`,`sub_command`)
) ENGINE=InnoDB AUTO_INCREMENT=134 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `commands_old`
--

DROP TABLE IF EXISTS `commands_old`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `commands_old` (
  `command` varchar(20) NOT NULL DEFAULT '',
  `access` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `description` varchar(255) DEFAULT NULL,
  PRIMARY KEY (`command`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `completed_shared_task_activity_state`
--

DROP TABLE IF EXISTS `completed_shared_task_activity_state`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `completed_shared_task_activity_state` (
  `shared_task_id` bigint(20) NOT NULL,
  `activity_id` int(11) NOT NULL,
  `done_count` int(11) DEFAULT NULL,
  `updated_time` datetime DEFAULT NULL,
  `completed_time` datetime DEFAULT NULL,
  PRIMARY KEY (`shared_task_id`,`activity_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `completed_shared_task_members`
--

DROP TABLE IF EXISTS `completed_shared_task_members`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `completed_shared_task_members` (
  `shared_task_id` bigint(20) NOT NULL,
  `character_id` bigint(20) NOT NULL,
  `is_leader` tinyint(4) DEFAULT NULL,
  PRIMARY KEY (`shared_task_id`,`character_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `completed_shared_tasks`
--

DROP TABLE IF EXISTS `completed_shared_tasks`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `completed_shared_tasks` (
  `id` bigint(20) NOT NULL,
  `task_id` int(11) DEFAULT NULL,
  `accepted_time` datetime DEFAULT NULL,
  `expire_time` datetime DEFAULT NULL,
  `completion_time` datetime DEFAULT NULL,
  `is_locked` tinyint(1) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `completed_tasks`
--

DROP TABLE IF EXISTS `completed_tasks`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `completed_tasks` (
  `charid` int(11) unsigned NOT NULL DEFAULT 0,
  `completedtime` int(11) unsigned NOT NULL DEFAULT 0,
  `taskid` int(11) unsigned NOT NULL DEFAULT 0,
  `activityid` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`charid`,`completedtime`,`taskid`,`activityid`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `content_flags`
--

DROP TABLE IF EXISTS `content_flags`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `content_flags` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `flag_name` varchar(75) DEFAULT NULL,
  `enabled` tinyint(4) DEFAULT NULL,
  `notes` text DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `core_version`
--

DROP TABLE IF EXISTS `core_version`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `core_version` (
  `rev` text NOT NULL
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `cust_sound_files`
--

DROP TABLE IF EXISTS `cust_sound_files`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `cust_sound_files` (
  `filename` varchar(100) NOT NULL,
  `length` int(11) DEFAULT NULL,
  `copyright` varchar(100) DEFAULT NULL,
  `details` varchar(100) DEFAULT NULL,
  `title` varchar(100) DEFAULT NULL,
  `artist` varchar(100) DEFAULT NULL,
  `year` varchar(25) DEFAULT NULL,
  PRIMARY KEY (`filename`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `custom_faction_mappings`
--

DROP TABLE IF EXISTS `custom_faction_mappings`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `custom_faction_mappings` (
  `old_faction` int(11) NOT NULL DEFAULT 0,
  `new_faction` int(11) DEFAULT NULL,
  PRIMARY KEY (`old_faction`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `damageshieldtypes`
--

DROP TABLE IF EXISTS `damageshieldtypes`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `damageshieldtypes` (
  `spellid` int(10) unsigned NOT NULL DEFAULT 0,
  `type` tinyint(3) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`spellid`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `data_buckets`
--

DROP TABLE IF EXISTS `data_buckets`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `data_buckets` (
  `id` bigint(11) unsigned NOT NULL AUTO_INCREMENT,
  `key` varchar(100) DEFAULT NULL,
  `value` text DEFAULT NULL,
  `expires` int(11) unsigned DEFAULT 0,
  `character_id` bigint(11) NOT NULL DEFAULT 0,
  `npc_id` bigint(11) NOT NULL DEFAULT 0,
  `bot_id` bigint(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `keys` (`key`,`character_id`,`npc_id`,`bot_id`)
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `db_str`
--

DROP TABLE IF EXISTS `db_str`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `db_str` (
  `id` int(10) NOT NULL,
  `type` int(10) NOT NULL,
  `value` text NOT NULL,
  PRIMARY KEY (`id`,`type`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `db_version`
--

DROP TABLE IF EXISTS `db_version`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `db_version` (
  `version` int(11) DEFAULT 0,
  `bots_version` int(11) DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `discord_webhooks`
--

DROP TABLE IF EXISTS `discord_webhooks`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `discord_webhooks` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `webhook_name` varchar(100) DEFAULT NULL,
  `webhook_url` varchar(255) DEFAULT NULL,
  `created_at` datetime DEFAULT NULL,
  `deleted_at` datetime DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `discovered_items`
--

DROP TABLE IF EXISTS `discovered_items`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `discovered_items` (
  `item_id` int(11) unsigned NOT NULL DEFAULT 0,
  `char_name` varchar(64) NOT NULL DEFAULT '',
  `discovered_date` int(11) unsigned NOT NULL DEFAULT 0,
  `account_status` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`item_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `doors`
--

DROP TABLE IF EXISTS `doors`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `doors` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `doorid` smallint(4) NOT NULL DEFAULT 0,
  `zone` varchar(32) DEFAULT NULL,
  `version` smallint(5) NOT NULL DEFAULT 0,
  `name` varchar(32) NOT NULL DEFAULT '',
  `pos_y` float NOT NULL DEFAULT 0,
  `pos_x` float NOT NULL DEFAULT 0,
  `pos_z` float NOT NULL DEFAULT 0,
  `heading` float NOT NULL DEFAULT 0,
  `opentype` smallint(4) NOT NULL DEFAULT 0,
  `guild` smallint(4) NOT NULL DEFAULT 0,
  `lockpick` smallint(4) NOT NULL DEFAULT 0,
  `keyitem` int(11) NOT NULL DEFAULT 0,
  `nokeyring` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `triggerdoor` smallint(4) NOT NULL DEFAULT 0,
  `triggertype` smallint(4) NOT NULL DEFAULT 0,
  `disable_timer` tinyint(2) NOT NULL DEFAULT 0,
  `doorisopen` smallint(4) NOT NULL DEFAULT 0,
  `door_param` int(4) NOT NULL DEFAULT 0,
  `dest_zone` varchar(32) DEFAULT 'NONE',
  `dest_instance` int(10) unsigned NOT NULL DEFAULT 0,
  `dest_x` float NOT NULL DEFAULT 0,
  `dest_y` float NOT NULL DEFAULT 0,
  `dest_z` float NOT NULL DEFAULT 0,
  `dest_heading` float NOT NULL DEFAULT 0,
  `invert_state` int(11) NOT NULL DEFAULT 0,
  `incline` int(11) NOT NULL DEFAULT 0,
  `size` smallint(5) unsigned NOT NULL DEFAULT 100,
  `buffer` float NOT NULL DEFAULT 0,
  `client_version_mask` int(10) unsigned NOT NULL DEFAULT 4294967295,
  `is_ldon_door` smallint(5) NOT NULL DEFAULT 0,
  `close_timer_ms` smallint(8) unsigned NOT NULL DEFAULT 5000,
  `dz_switch_id` int(11) NOT NULL DEFAULT 0,
  `min_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `max_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `content_flags` varchar(100) DEFAULT NULL,
  `content_flags_disabled` varchar(100) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `DoorIndex` (`zone`,`doorid`,`version`)
) ENGINE=MyISAM AUTO_INCREMENT=36670 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `dynamic_zone_members`
--

DROP TABLE IF EXISTS `dynamic_zone_members`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `dynamic_zone_members` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `dynamic_zone_id` int(10) unsigned NOT NULL DEFAULT 0,
  `character_id` int(10) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `dynamic_zone_id_character_id` (`dynamic_zone_id`,`character_id`),
  KEY `character_id` (`character_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `dynamic_zone_templates`
--

DROP TABLE IF EXISTS `dynamic_zone_templates`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `dynamic_zone_templates` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `zone_id` int(11) NOT NULL DEFAULT 0,
  `zone_version` int(11) NOT NULL DEFAULT 0,
  `name` varchar(128) NOT NULL DEFAULT '',
  `min_players` int(11) NOT NULL DEFAULT 0,
  `max_players` int(11) NOT NULL DEFAULT 0,
  `duration_seconds` int(11) NOT NULL DEFAULT 0,
  `dz_switch_id` int(11) NOT NULL DEFAULT 0,
  `compass_zone_id` int(11) NOT NULL DEFAULT 0,
  `compass_x` float NOT NULL DEFAULT 0,
  `compass_y` float NOT NULL DEFAULT 0,
  `compass_z` float NOT NULL DEFAULT 0,
  `return_zone_id` int(11) NOT NULL DEFAULT 0,
  `return_x` float NOT NULL DEFAULT 0,
  `return_y` float NOT NULL DEFAULT 0,
  `return_z` float NOT NULL DEFAULT 0,
  `return_h` float NOT NULL DEFAULT 0,
  `override_zone_in` tinyint(4) NOT NULL DEFAULT 0,
  `zone_in_x` float NOT NULL DEFAULT 0,
  `zone_in_y` float NOT NULL DEFAULT 0,
  `zone_in_z` float NOT NULL DEFAULT 0,
  `zone_in_h` float NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `dynamic_zones`
--

DROP TABLE IF EXISTS `dynamic_zones`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `dynamic_zones` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `instance_id` int(10) NOT NULL DEFAULT 0,
  `type` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `uuid` varchar(36) CHARACTER SET latin1 COLLATE latin1_swedish_ci NOT NULL,
  `name` varchar(128) CHARACTER SET latin1 COLLATE latin1_swedish_ci NOT NULL DEFAULT '',
  `leader_id` int(10) unsigned NOT NULL DEFAULT 0,
  `min_players` int(10) unsigned NOT NULL DEFAULT 0,
  `max_players` int(10) unsigned NOT NULL DEFAULT 0,
  `dz_switch_id` int(11) NOT NULL DEFAULT 0,
  `compass_zone_id` int(10) unsigned NOT NULL DEFAULT 0,
  `compass_x` float NOT NULL DEFAULT 0,
  `compass_y` float NOT NULL DEFAULT 0,
  `compass_z` float NOT NULL DEFAULT 0,
  `safe_return_zone_id` int(10) unsigned NOT NULL DEFAULT 0,
  `safe_return_x` float NOT NULL DEFAULT 0,
  `safe_return_y` float NOT NULL DEFAULT 0,
  `safe_return_z` float NOT NULL DEFAULT 0,
  `safe_return_heading` float NOT NULL DEFAULT 0,
  `zone_in_x` float NOT NULL DEFAULT 0,
  `zone_in_y` float NOT NULL DEFAULT 0,
  `zone_in_z` float NOT NULL DEFAULT 0,
  `zone_in_heading` float NOT NULL DEFAULT 0,
  `has_zone_in` tinyint(3) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `instance_id` (`instance_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `editor_values`
--

DROP TABLE IF EXISTS `editor_values`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `editor_values` (
  `name` varchar(64) NOT NULL,
  `location` mediumint(9) NOT NULL,
  `bytes` smallint(2) NOT NULL,
  `type` varchar(10) NOT NULL,
  `source` enum('table','profile') NOT NULL DEFAULT 'profile',
  `update` enum('table','profile','both','none') NOT NULL DEFAULT 'profile',
  `source_sql` text NOT NULL,
  `source_return` varchar(20) NOT NULL,
  `update_sql` text NOT NULL,
  PRIMARY KEY (`name`)
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `eqbnews`
--

DROP TABLE IF EXISTS `eqbnews`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `eqbnews` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `date` date DEFAULT NULL,
  `title` varchar(250) NOT NULL DEFAULT '',
  `content` text NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=MyISAM AUTO_INCREMENT=13 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `eqtime`
--

DROP TABLE IF EXISTS `eqtime`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `eqtime` (
  `minute` tinyint(4) NOT NULL DEFAULT 0,
  `hour` tinyint(4) NOT NULL DEFAULT 0,
  `day` tinyint(4) NOT NULL DEFAULT 0,
  `month` tinyint(4) NOT NULL DEFAULT 0,
  `year` int(4) NOT NULL DEFAULT 0,
  `realtime` int(11) NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `expedition_lockouts`
--

DROP TABLE IF EXISTS `expedition_lockouts`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `expedition_lockouts` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `expedition_id` int(10) unsigned NOT NULL,
  `event_name` varchar(256) NOT NULL,
  `expire_time` datetime NOT NULL DEFAULT current_timestamp(),
  `duration` int(10) unsigned NOT NULL DEFAULT 0,
  `from_expedition_uuid` varchar(36) NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `expedition_id_event_name` (`expedition_id`,`event_name`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `expeditions`
--

DROP TABLE IF EXISTS `expeditions`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `expeditions` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `dynamic_zone_id` int(10) unsigned NOT NULL DEFAULT 0,
  `add_replay_on_join` tinyint(3) unsigned NOT NULL DEFAULT 1,
  `is_locked` tinyint(3) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `dynamic_zone_id` (`dynamic_zone_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `faction_association`
--

DROP TABLE IF EXISTS `faction_association`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `faction_association` (
  `id` int(10) NOT NULL,
  `id_1` int(10) NOT NULL DEFAULT 0,
  `mod_1` float NOT NULL DEFAULT 0,
  `id_2` int(10) NOT NULL DEFAULT 0,
  `mod_2` float NOT NULL DEFAULT 0,
  `id_3` int(10) NOT NULL DEFAULT 0,
  `mod_3` float NOT NULL DEFAULT 0,
  `id_4` int(10) NOT NULL DEFAULT 0,
  `mod_4` float NOT NULL DEFAULT 0,
  `id_5` int(10) NOT NULL DEFAULT 0,
  `mod_5` float NOT NULL DEFAULT 0,
  `id_6` int(10) NOT NULL DEFAULT 0,
  `mod_6` float NOT NULL DEFAULT 0,
  `id_7` int(10) NOT NULL DEFAULT 0,
  `mod_7` float NOT NULL DEFAULT 0,
  `id_8` int(10) NOT NULL DEFAULT 0,
  `mod_8` float NOT NULL DEFAULT 0,
  `id_9` int(10) NOT NULL DEFAULT 0,
  `mod_9` float NOT NULL DEFAULT 0,
  `id_10` int(10) NOT NULL DEFAULT 0,
  `mod_10` float NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `faction_base_data`
--

DROP TABLE IF EXISTS `faction_base_data`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `faction_base_data` (
  `client_faction_id` smallint(6) NOT NULL,
  `min` smallint(6) DEFAULT -2000,
  `max` smallint(6) DEFAULT 2000,
  `unk_hero1` smallint(6) DEFAULT NULL,
  `unk_hero2` smallint(6) DEFAULT NULL,
  `unk_hero3` smallint(6) DEFAULT NULL,
  PRIMARY KEY (`client_faction_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `faction_list`
--

DROP TABLE IF EXISTS `faction_list`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `faction_list` (
  `id` int(11) NOT NULL,
  `name` varchar(50) NOT NULL DEFAULT '',
  `base` smallint(6) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `id` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `faction_list_mod`
--

DROP TABLE IF EXISTS `faction_list_mod`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `faction_list_mod` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `faction_id` int(10) unsigned NOT NULL,
  `mod` smallint(6) NOT NULL,
  `mod_name` varchar(16) NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `faction_id_mod_name` (`faction_id`,`mod_name`)
) ENGINE=InnoDB AUTO_INCREMENT=11686 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `faction_list_mod_prefix`
--

DROP TABLE IF EXISTS `faction_list_mod_prefix`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `faction_list_mod_prefix` (
  `id` int(10) unsigned NOT NULL DEFAULT 0,
  `faction_id` int(10) unsigned NOT NULL,
  `mod` smallint(6) NOT NULL,
  `mod_name` varchar(16) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `faction_list_prefix`
--

DROP TABLE IF EXISTS `faction_list_prefix`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `faction_list_prefix` (
  `id` int(11) NOT NULL DEFAULT 0,
  `name` varchar(50) CHARACTER SET utf8mb3 COLLATE utf8mb3_uca1400_ai_ci NOT NULL DEFAULT '',
  `base` smallint(6) NOT NULL DEFAULT 0,
  `mod_c1` smallint(6) NOT NULL DEFAULT 0,
  `mod_c2` smallint(6) NOT NULL DEFAULT 0,
  `mod_c3` smallint(6) NOT NULL DEFAULT 0,
  `mod_c4` smallint(6) NOT NULL DEFAULT 0,
  `mod_c5` smallint(6) NOT NULL DEFAULT 0,
  `mod_c6` smallint(6) NOT NULL DEFAULT 0,
  `mod_c7` smallint(6) NOT NULL DEFAULT 0,
  `mod_c8` smallint(6) NOT NULL DEFAULT 0,
  `mod_c9` smallint(6) NOT NULL DEFAULT 0,
  `mod_c10` smallint(6) NOT NULL DEFAULT 0,
  `mod_c11` smallint(6) NOT NULL DEFAULT 0,
  `mod_c12` smallint(6) NOT NULL DEFAULT 0,
  `mod_c13` smallint(6) NOT NULL DEFAULT 0,
  `mod_c14` smallint(6) NOT NULL DEFAULT 0,
  `mod_c15` smallint(6) NOT NULL DEFAULT 0,
  `mod_c16` smallint(6) NOT NULL DEFAULT 0,
  `mod_r1` smallint(6) NOT NULL DEFAULT 0,
  `mod_r2` smallint(6) NOT NULL DEFAULT 0,
  `mod_r3` smallint(6) NOT NULL DEFAULT 0,
  `mod_r4` smallint(6) NOT NULL DEFAULT 0,
  `mod_r5` smallint(6) NOT NULL DEFAULT 0,
  `mod_r6` smallint(6) NOT NULL DEFAULT 0,
  `mod_r7` smallint(6) NOT NULL DEFAULT 0,
  `mod_r8` smallint(6) NOT NULL DEFAULT 0,
  `mod_r9` smallint(6) NOT NULL DEFAULT 0,
  `mod_r10` smallint(6) NOT NULL DEFAULT 0,
  `mod_r11` smallint(6) NOT NULL DEFAULT 0,
  `mod_r12` smallint(6) NOT NULL DEFAULT 0,
  `mod_r14` smallint(6) NOT NULL DEFAULT 0,
  `mod_r60` smallint(6) NOT NULL DEFAULT 0,
  `mod_r75` smallint(6) NOT NULL DEFAULT 0,
  `mod_r108` smallint(6) NOT NULL DEFAULT 0,
  `mod_r120` smallint(6) NOT NULL DEFAULT 0,
  `mod_r128` smallint(6) NOT NULL DEFAULT 0,
  `mod_r130` smallint(6) NOT NULL DEFAULT 0,
  `mod_r161` smallint(6) NOT NULL DEFAULT 0,
  `mod_r330` smallint(6) NOT NULL DEFAULT 0,
  `mod_d140` smallint(6) NOT NULL DEFAULT 0,
  `mod_d201` smallint(6) NOT NULL DEFAULT 0,
  `mod_d202` smallint(6) NOT NULL DEFAULT 0,
  `mod_d203` smallint(6) NOT NULL DEFAULT 0,
  `mod_d204` smallint(6) NOT NULL DEFAULT 0,
  `mod_d205` smallint(6) NOT NULL DEFAULT 0,
  `mod_d206` smallint(6) NOT NULL DEFAULT 0,
  `mod_d207` smallint(6) NOT NULL DEFAULT 0,
  `mod_d208` smallint(6) NOT NULL DEFAULT 0,
  `mod_d209` smallint(6) NOT NULL DEFAULT 0,
  `mod_d210` smallint(6) NOT NULL DEFAULT 0,
  `mod_d211` smallint(6) NOT NULL DEFAULT 0,
  `mod_d212` smallint(6) NOT NULL DEFAULT 0,
  `mod_d213` smallint(6) NOT NULL DEFAULT 0,
  `mod_d214` smallint(6) NOT NULL DEFAULT 0,
  `mod_d215` smallint(6) NOT NULL DEFAULT 0,
  `mod_d216` smallint(6) NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `faction_values`
--

DROP TABLE IF EXISTS `faction_values`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `faction_values` (
  `char_id` int(4) NOT NULL DEFAULT 0,
  `faction_id` int(4) NOT NULL DEFAULT 0,
  `current_value` smallint(6) NOT NULL DEFAULT 0,
  `temp` tinyint(3) NOT NULL DEFAULT 0,
  PRIMARY KEY (`char_id`,`faction_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `faction_values_prefix`
--

DROP TABLE IF EXISTS `faction_values_prefix`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `faction_values_prefix` (
  `char_id` int(4) NOT NULL DEFAULT 0,
  `faction_id` int(4) NOT NULL DEFAULT 0,
  `current_value` smallint(6) NOT NULL DEFAULT 0,
  `temp` tinyint(3) NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `fear_hints`
--

DROP TABLE IF EXISTS `fear_hints`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `fear_hints` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `zone` varchar(16) NOT NULL DEFAULT '',
  `x` float NOT NULL DEFAULT 0,
  `y` float NOT NULL DEFAULT 0,
  `z` float NOT NULL DEFAULT 0,
  `forced` tinyint(4) NOT NULL DEFAULT 0,
  `disjoint` tinyint(4) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `zone` (`zone`,`x`,`y`,`z`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `fishing`
--

DROP TABLE IF EXISTS `fishing`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `fishing` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `zoneid` int(4) NOT NULL DEFAULT 0,
  `Itemid` int(11) NOT NULL DEFAULT 0,
  `skill_level` smallint(6) NOT NULL DEFAULT 0,
  `chance` smallint(6) NOT NULL DEFAULT 0,
  `npc_id` int(11) NOT NULL DEFAULT 0,
  `npc_chance` int(11) NOT NULL DEFAULT 0,
  `min_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `max_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `content_flags` varchar(100) DEFAULT NULL,
  `content_flags_disabled` varchar(100) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=MyISAM AUTO_INCREMENT=159 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `forage`
--

DROP TABLE IF EXISTS `forage`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `forage` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `zoneid` int(4) NOT NULL DEFAULT 0,
  `Itemid` int(11) NOT NULL DEFAULT 0,
  `level` smallint(6) NOT NULL DEFAULT 0,
  `chance` smallint(6) NOT NULL DEFAULT 0,
  `min_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `max_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `content_flags` varchar(100) DEFAULT NULL,
  `content_flags_disabled` varchar(100) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=MyISAM AUTO_INCREMENT=488 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `friends`
--

DROP TABLE IF EXISTS `friends`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `friends` (
  `charid` int(10) unsigned NOT NULL DEFAULT 0,
  `type` tinyint(1) unsigned NOT NULL DEFAULT 1 COMMENT '1 = Friend, 0 = Ignore',
  `name` varchar(64) NOT NULL DEFAULT '',
  PRIMARY KEY (`charid`,`type`,`name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `global_loot`
--

DROP TABLE IF EXISTS `global_loot`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `global_loot` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `description` varchar(255) DEFAULT NULL,
  `loottable_id` int(11) NOT NULL,
  `enabled` tinyint(4) NOT NULL DEFAULT 1,
  `min_level` int(11) NOT NULL DEFAULT 0,
  `max_level` int(11) NOT NULL DEFAULT 0,
  `rare` tinyint(4) DEFAULT NULL,
  `raid` tinyint(4) DEFAULT NULL,
  `race` mediumtext DEFAULT NULL,
  `class` mediumtext DEFAULT NULL,
  `bodytype` mediumtext DEFAULT NULL,
  `zone` mediumtext DEFAULT NULL,
  `hot_zone` tinyint(4) DEFAULT NULL,
  `min_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `max_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `content_flags` varchar(100) DEFAULT NULL,
  `content_flags_disabled` varchar(100) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `gm_ips`
--

DROP TABLE IF EXISTS `gm_ips`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `gm_ips` (
  `name` varchar(64) NOT NULL DEFAULT '',
  `account_id` int(11) NOT NULL DEFAULT 0,
  `ip_address` varchar(15) NOT NULL DEFAULT '',
  UNIQUE KEY `account_id` (`account_id`,`ip_address`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `goallists_backup_9_25_2022`
--

DROP TABLE IF EXISTS `goallists_backup_9_25_2022`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `goallists_backup_9_25_2022` (
  `listid` int(10) unsigned NOT NULL DEFAULT 0,
  `entry` int(10) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`listid`,`entry`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `graveyard`
--

DROP TABLE IF EXISTS `graveyard`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `graveyard` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `zone_id` int(4) NOT NULL DEFAULT 0,
  `x` float NOT NULL DEFAULT 0,
  `y` float NOT NULL DEFAULT 0,
  `z` float NOT NULL DEFAULT 0,
  `heading` float NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `zone_id_nonunique` (`zone_id`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `grid`
--

DROP TABLE IF EXISTS `grid`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `grid` (
  `id` int(10) NOT NULL DEFAULT 0,
  `zoneid` int(10) NOT NULL DEFAULT 0,
  `type` int(10) NOT NULL DEFAULT 0,
  `type2` int(10) NOT NULL DEFAULT 0,
  PRIMARY KEY (`zoneid`,`id`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `grid_entries`
--

DROP TABLE IF EXISTS `grid_entries`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `grid_entries` (
  `gridid` int(10) NOT NULL DEFAULT 0,
  `zoneid` int(10) NOT NULL DEFAULT 0,
  `number` int(10) NOT NULL DEFAULT 0,
  `x` float NOT NULL DEFAULT 0,
  `y` float NOT NULL DEFAULT 0,
  `z` float NOT NULL DEFAULT 0,
  `heading` float NOT NULL DEFAULT 0,
  `pause` int(10) NOT NULL DEFAULT 0,
  `centerpoint` tinyint(4) NOT NULL DEFAULT 0,
  PRIMARY KEY (`zoneid`,`gridid`,`number`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `ground_spawns`
--

DROP TABLE IF EXISTS `ground_spawns`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `ground_spawns` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `zoneid` int(10) unsigned NOT NULL DEFAULT 0,
  `version` smallint(5) NOT NULL DEFAULT 0,
  `max_x` float NOT NULL DEFAULT 2000,
  `max_y` float NOT NULL DEFAULT 2000,
  `max_z` float NOT NULL DEFAULT 10000,
  `min_x` float NOT NULL DEFAULT -2000,
  `min_y` float NOT NULL DEFAULT -2000,
  `heading` float NOT NULL DEFAULT 0,
  `name` varchar(16) NOT NULL DEFAULT '',
  `item` int(10) unsigned NOT NULL DEFAULT 0,
  `max_allowed` int(10) unsigned NOT NULL DEFAULT 1,
  `comment` varchar(255) NOT NULL DEFAULT '',
  `respawn_timer` int(11) unsigned NOT NULL DEFAULT 300,
  `fix_z` tinyint(1) unsigned NOT NULL DEFAULT 1,
  `min_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `max_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `content_flags` varchar(100) DEFAULT NULL,
  `content_flags_disabled` varchar(100) DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `zone` (`zoneid`)
) ENGINE=MyISAM AUTO_INCREMENT=159 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `group_id`
--

DROP TABLE IF EXISTS `group_id`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `group_id` (
  `group_id` int(11) unsigned NOT NULL DEFAULT 0,
  `name` varchar(64) NOT NULL DEFAULT '',
  `character_id` int(11) unsigned NOT NULL DEFAULT 0,
  `bot_id` int(11) unsigned NOT NULL DEFAULT 0,
  `merc_id` int(11) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`group_id`,`character_id`,`bot_id`,`merc_id`) USING BTREE
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `group_leaders`
--

DROP TABLE IF EXISTS `group_leaders`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `group_leaders` (
  `gid` int(4) NOT NULL DEFAULT 0,
  `leadername` varchar(64) NOT NULL DEFAULT '',
  `marknpc` varchar(64) NOT NULL DEFAULT '',
  `leadershipaa` tinyblob DEFAULT NULL,
  `maintank` varchar(64) NOT NULL DEFAULT '',
  `assist` varchar(64) NOT NULL DEFAULT '',
  `puller` varchar(64) NOT NULL DEFAULT '',
  `mentoree` varchar(64) NOT NULL,
  `mentor_percent` int(4) NOT NULL DEFAULT 0,
  PRIMARY KEY (`gid`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `group_leaders__`
--

DROP TABLE IF EXISTS `group_leaders__`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `group_leaders__` (
  `gid` int(4) NOT NULL DEFAULT 0,
  `leadername` varchar(64) NOT NULL DEFAULT '',
  `assist` varchar(64) NOT NULL DEFAULT '',
  `marknpc` varchar(64) NOT NULL DEFAULT '',
  `leadershipaa` tinyblob NOT NULL,
  `mentoree` varchar(64) NOT NULL,
  `mentor_percent` int(4) NOT NULL DEFAULT 0,
  PRIMARY KEY (`gid`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `guild_alliances`
--

DROP TABLE IF EXISTS `guild_alliances`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `guild_alliances` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `guildone` int(11) NOT NULL DEFAULT 0,
  `guildtwo` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `guild_bank`
--

DROP TABLE IF EXISTS `guild_bank`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `guild_bank` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `guildid` int(10) unsigned NOT NULL DEFAULT 0,
  `area` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `slot` int(4) unsigned NOT NULL DEFAULT 0,
  `itemid` int(10) unsigned NOT NULL DEFAULT 0,
  `qty` int(10) NOT NULL DEFAULT 0,
  `donator` varchar(64) DEFAULT NULL,
  `permissions` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `whofor` varchar(64) DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `guildid` (`guildid`),
  KEY `area` (`area`),
  KEY `slot` (`slot`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `guild_controllers`
--

DROP TABLE IF EXISTS `guild_controllers`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `guild_controllers` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `npc_id` int(11) NOT NULL DEFAULT 0,
  `owned_guild_id` int(11) NOT NULL DEFAULT 0,
  `zoneid` int(11) NOT NULL DEFAULT 0,
  `terrainarea` varchar(64) NOT NULL DEFAULT '',
  PRIMARY KEY (`id`),
  UNIQUE KEY `npc_id` (`npc_id`),
  UNIQUE KEY `zoneid` (`zoneid`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `guild_members`
--

DROP TABLE IF EXISTS `guild_members`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `guild_members` (
  `char_id` int(11) NOT NULL DEFAULT 0,
  `guild_id` mediumint(8) unsigned NOT NULL DEFAULT 0,
  `rank` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `tribute_enable` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `total_tribute` int(10) unsigned NOT NULL DEFAULT 0,
  `last_tribute` int(10) unsigned NOT NULL DEFAULT 0,
  `banker` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `public_note` text NOT NULL,
  `alt` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `online` tinyint(3) unsigned NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `guild_permissions`
--

DROP TABLE IF EXISTS `guild_permissions`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `guild_permissions` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `perm_id` int(11) NOT NULL DEFAULT 0,
  `guild_id` int(11) NOT NULL DEFAULT 0,
  `permission` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE KEY `perm_id_guild_id` (`perm_id`,`guild_id`) USING BTREE
) ENGINE=InnoDB AUTO_INCREMENT=1801 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `guild_ranks`
--

DROP TABLE IF EXISTS `guild_ranks`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `guild_ranks` (
  `guild_id` mediumint(8) unsigned NOT NULL DEFAULT 0,
  `rank` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `title` varchar(128) NOT NULL DEFAULT '',
  PRIMARY KEY (`guild_id`,`rank`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `guild_relations`
--

DROP TABLE IF EXISTS `guild_relations`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `guild_relations` (
  `guild1` mediumint(8) unsigned NOT NULL DEFAULT 0,
  `guild2` mediumint(8) unsigned NOT NULL DEFAULT 0,
  `relation` tinyint(4) NOT NULL DEFAULT 0,
  PRIMARY KEY (`guild1`,`guild2`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `guild_tributes`
--

DROP TABLE IF EXISTS `guild_tributes`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `guild_tributes` (
  `guild_id` int(11) unsigned NOT NULL DEFAULT 0,
  `tribute_id_1` int(11) unsigned NOT NULL DEFAULT 0,
  `tribute_id_1_tier` int(11) unsigned NOT NULL DEFAULT 0,
  `tribute_id_2` int(11) unsigned NOT NULL DEFAULT 0,
  `tribute_id_2_tier` int(11) unsigned NOT NULL DEFAULT 0,
  `time_remaining` int(11) unsigned NOT NULL DEFAULT 0,
  `enabled` int(11) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`guild_id`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `guilds`
--

DROP TABLE IF EXISTS `guilds`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `guilds` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(32) NOT NULL DEFAULT '',
  `leader` int(11) NOT NULL DEFAULT 0,
  `minstatus` smallint(5) NOT NULL DEFAULT 0,
  `motd` text NOT NULL,
  `tribute` int(10) unsigned NOT NULL DEFAULT 0,
  `motd_setter` varchar(64) NOT NULL DEFAULT '',
  `channel` varchar(128) NOT NULL DEFAULT '',
  `url` varchar(512) NOT NULL DEFAULT '',
  `favor` int(10) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `name` (`name`),
  UNIQUE KEY `leader` (`leader`)
) ENGINE=InnoDB AUTO_INCREMENT=357 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `horses`
--

DROP TABLE IF EXISTS `horses`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `horses` (
  `id` int(20) NOT NULL AUTO_INCREMENT,
  `filename` varchar(32) NOT NULL DEFAULT '',
  `race` smallint(3) NOT NULL DEFAULT 216,
  `gender` tinyint(1) NOT NULL DEFAULT 0,
  `texture` tinyint(2) NOT NULL DEFAULT 0,
  `mountspeed` float(4,2) NOT NULL DEFAULT 0.75,
  `notes` varchar(64) DEFAULT 'Notes',
  PRIMARY KEY (`id`),
  UNIQUE KEY `filename` (`filename`)
) ENGINE=InnoDB AUTO_INCREMENT=86 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `instance_list`
--

DROP TABLE IF EXISTS `instance_list`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `instance_list` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `zone` int(11) unsigned NOT NULL DEFAULT 0,
  `version` tinyint(4) unsigned NOT NULL DEFAULT 0,
  `is_global` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `start_time` int(11) unsigned NOT NULL DEFAULT 0,
  `duration` int(11) unsigned NOT NULL DEFAULT 0,
  `never_expires` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `notes` varchar(50) NOT NULL DEFAULT '',
  PRIMARY KEY (`id`),
  UNIQUE KEY `id` (`id`),
  KEY `id_2` (`id`)
) ENGINE=MyISAM AUTO_INCREMENT=32 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `instance_list_player`
--

DROP TABLE IF EXISTS `instance_list_player`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `instance_list_player` (
  `id` int(11) unsigned NOT NULL DEFAULT 0,
  `charid` int(11) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`charid`,`id`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `inventory`
--

DROP TABLE IF EXISTS `inventory`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `inventory` (
  `charid` int(11) unsigned NOT NULL DEFAULT 0,
  `slotid` mediumint(7) unsigned NOT NULL DEFAULT 0,
  `itemid` int(11) unsigned DEFAULT 0,
  `charges` smallint(5) unsigned DEFAULT 0,
  `color` int(11) unsigned NOT NULL DEFAULT 0,
  `augslot1` mediumint(7) unsigned NOT NULL DEFAULT 0,
  `augslot2` mediumint(7) unsigned NOT NULL DEFAULT 0,
  `augslot3` mediumint(7) unsigned NOT NULL DEFAULT 0,
  `augslot4` mediumint(7) unsigned NOT NULL DEFAULT 0,
  `augslot5` mediumint(7) unsigned DEFAULT 0,
  `augslot6` mediumint(7) NOT NULL DEFAULT 0,
  `instnodrop` tinyint(1) unsigned NOT NULL DEFAULT 0,
  `custom_data` text DEFAULT NULL,
  `ornamenticon` int(11) unsigned NOT NULL DEFAULT 0,
  `ornamentidfile` int(11) unsigned NOT NULL DEFAULT 0,
  `ornament_hero_model` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`charid`,`slotid`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `inventory_snapshots`
--

DROP TABLE IF EXISTS `inventory_snapshots`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `inventory_snapshots` (
  `time_index` int(11) unsigned NOT NULL DEFAULT 0,
  `charid` int(11) unsigned NOT NULL DEFAULT 0,
  `slotid` mediumint(7) unsigned NOT NULL DEFAULT 0,
  `itemid` int(11) unsigned DEFAULT 0,
  `charges` smallint(3) unsigned DEFAULT 0,
  `color` int(11) unsigned NOT NULL DEFAULT 0,
  `augslot1` mediumint(7) unsigned NOT NULL DEFAULT 0,
  `augslot2` mediumint(7) unsigned NOT NULL DEFAULT 0,
  `augslot3` mediumint(7) unsigned NOT NULL DEFAULT 0,
  `augslot4` mediumint(7) unsigned NOT NULL DEFAULT 0,
  `augslot5` mediumint(7) unsigned DEFAULT 0,
  `augslot6` mediumint(7) NOT NULL DEFAULT 0,
  `instnodrop` tinyint(1) unsigned NOT NULL DEFAULT 0,
  `custom_data` text DEFAULT NULL,
  `ornamenticon` int(11) unsigned NOT NULL DEFAULT 0,
  `ornamentidfile` int(11) unsigned NOT NULL DEFAULT 0,
  `ornament_hero_model` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`time_index`,`charid`,`slotid`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `inventory_snapshots_v1_bak`
--

DROP TABLE IF EXISTS `inventory_snapshots_v1_bak`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `inventory_snapshots_v1_bak` (
  `time_index` int(11) unsigned NOT NULL DEFAULT 0,
  `charid` int(11) unsigned NOT NULL DEFAULT 0,
  `slotid` mediumint(7) unsigned NOT NULL DEFAULT 0,
  `itemid` int(11) unsigned DEFAULT 0,
  `charges` smallint(3) unsigned DEFAULT 0,
  `color` int(11) unsigned NOT NULL DEFAULT 0,
  `augslot1` mediumint(7) unsigned NOT NULL DEFAULT 0,
  `augslot2` mediumint(7) unsigned NOT NULL DEFAULT 0,
  `augslot3` mediumint(7) unsigned NOT NULL DEFAULT 0,
  `augslot4` mediumint(7) unsigned NOT NULL DEFAULT 0,
  `augslot5` mediumint(7) unsigned DEFAULT 0,
  `augslot6` mediumint(7) NOT NULL DEFAULT 0,
  `instnodrop` tinyint(1) unsigned NOT NULL DEFAULT 0,
  `custom_data` text DEFAULT NULL,
  `ornamenticon` int(11) unsigned NOT NULL DEFAULT 0,
  `ornamentidfile` int(11) unsigned NOT NULL DEFAULT 0,
  `ornament_hero_model` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`time_index`,`charid`,`slotid`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `inventory_versions`
--

DROP TABLE IF EXISTS `inventory_versions`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `inventory_versions` (
  `version` int(11) unsigned NOT NULL DEFAULT 0,
  `step` int(11) unsigned NOT NULL DEFAULT 0,
  `bot_step` int(11) unsigned NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `ip_exemptions`
--

DROP TABLE IF EXISTS `ip_exemptions`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `ip_exemptions` (
  `exemption_id` int(11) NOT NULL AUTO_INCREMENT,
  `exemption_ip` varchar(255) DEFAULT NULL,
  `exemption_amount` int(11) DEFAULT NULL,
  PRIMARY KEY (`exemption_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `items`
--

DROP TABLE IF EXISTS `items`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `items` (
  `id` int(11) NOT NULL DEFAULT 0,
  `minstatus` smallint(5) NOT NULL DEFAULT 0,
  `Name` varchar(64) NOT NULL DEFAULT '',
  `aagi` int(11) NOT NULL DEFAULT 0,
  `ac` int(11) NOT NULL DEFAULT 0,
  `accuracy` int(11) NOT NULL DEFAULT 0,
  `acha` int(11) NOT NULL DEFAULT 0,
  `adex` int(11) NOT NULL DEFAULT 0,
  `aint` int(11) NOT NULL DEFAULT 0,
  `artifactflag` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `asta` int(11) NOT NULL DEFAULT 0,
  `astr` int(11) NOT NULL DEFAULT 0,
  `attack` int(11) NOT NULL DEFAULT 0,
  `augrestrict` int(11) NOT NULL DEFAULT 0,
  `augslot1type` tinyint(3) NOT NULL DEFAULT 0,
  `augslot1visible` tinyint(3) DEFAULT NULL,
  `augslot2type` tinyint(3) NOT NULL DEFAULT 0,
  `augslot2visible` tinyint(3) DEFAULT NULL,
  `augslot3type` tinyint(3) NOT NULL DEFAULT 0,
  `augslot3visible` tinyint(3) DEFAULT NULL,
  `augslot4type` tinyint(3) NOT NULL DEFAULT 0,
  `augslot4visible` tinyint(3) DEFAULT NULL,
  `augslot5type` tinyint(3) NOT NULL DEFAULT 0,
  `augslot5visible` tinyint(3) DEFAULT NULL,
  `augslot6type` tinyint(3) NOT NULL DEFAULT 0,
  `augslot6visible` tinyint(3) NOT NULL DEFAULT 0,
  `augtype` int(11) NOT NULL DEFAULT 0,
  `avoidance` int(11) NOT NULL DEFAULT 0,
  `awis` int(11) NOT NULL DEFAULT 0,
  `bagsize` int(11) NOT NULL DEFAULT 0,
  `bagslots` int(11) NOT NULL DEFAULT 0,
  `bagtype` int(11) NOT NULL DEFAULT 0,
  `bagwr` int(11) NOT NULL DEFAULT 0,
  `banedmgamt` int(11) NOT NULL DEFAULT 0,
  `banedmgraceamt` int(11) NOT NULL DEFAULT 0,
  `banedmgbody` int(11) NOT NULL DEFAULT 0,
  `banedmgrace` int(11) NOT NULL DEFAULT 0,
  `bardtype` int(11) NOT NULL DEFAULT 0,
  `bardvalue` int(11) NOT NULL DEFAULT 0,
  `book` int(11) NOT NULL DEFAULT 0,
  `casttime` int(11) NOT NULL DEFAULT 0,
  `casttime_` int(11) NOT NULL DEFAULT 0,
  `charmfile` varchar(32) NOT NULL DEFAULT '',
  `charmfileid` varchar(32) NOT NULL DEFAULT '',
  `classes` int(11) NOT NULL DEFAULT 0,
  `color` int(10) unsigned NOT NULL DEFAULT 0,
  `combateffects` varchar(10) NOT NULL DEFAULT '',
  `extradmgskill` int(11) NOT NULL DEFAULT 0,
  `extradmgamt` int(11) NOT NULL DEFAULT 0,
  `price` int(11) NOT NULL DEFAULT 0,
  `cr` int(11) NOT NULL DEFAULT 0,
  `damage` int(11) NOT NULL DEFAULT 0,
  `damageshield` int(11) NOT NULL DEFAULT 0,
  `deity` int(11) NOT NULL DEFAULT 0,
  `delay` int(11) NOT NULL DEFAULT 0,
  `augdistiller` int(11) NOT NULL DEFAULT 0,
  `dotshielding` int(11) NOT NULL DEFAULT 0,
  `dr` int(11) NOT NULL DEFAULT 0,
  `clicktype` int(11) NOT NULL DEFAULT 0,
  `clicklevel2` int(11) NOT NULL DEFAULT 0,
  `elemdmgtype` int(11) NOT NULL DEFAULT 0,
  `elemdmgamt` int(11) NOT NULL DEFAULT 0,
  `endur` int(11) NOT NULL DEFAULT 0,
  `factionamt1` int(11) NOT NULL DEFAULT 0,
  `factionamt2` int(11) NOT NULL DEFAULT 0,
  `factionamt3` int(11) NOT NULL DEFAULT 0,
  `factionamt4` int(11) NOT NULL DEFAULT 0,
  `factionmod1` int(11) NOT NULL DEFAULT 0,
  `factionmod2` int(11) NOT NULL DEFAULT 0,
  `factionmod3` int(11) NOT NULL DEFAULT 0,
  `factionmod4` int(11) NOT NULL DEFAULT 0,
  `filename` varchar(32) NOT NULL DEFAULT '',
  `focuseffect` int(11) NOT NULL DEFAULT 0,
  `fr` int(11) NOT NULL DEFAULT 0,
  `fvnodrop` int(11) NOT NULL DEFAULT 0,
  `haste` int(11) NOT NULL DEFAULT 0,
  `clicklevel` int(11) NOT NULL DEFAULT 0,
  `hp` int(11) NOT NULL DEFAULT 0,
  `regen` int(11) NOT NULL DEFAULT 0,
  `icon` int(11) NOT NULL DEFAULT 0,
  `idfile` varchar(30) NOT NULL DEFAULT '',
  `itemclass` int(11) NOT NULL DEFAULT 0,
  `itemtype` int(11) NOT NULL DEFAULT 0,
  `ldonprice` int(11) NOT NULL DEFAULT 0,
  `ldontheme` int(11) NOT NULL DEFAULT 0,
  `ldonsold` int(11) NOT NULL DEFAULT 0,
  `light` int(11) NOT NULL DEFAULT 0,
  `lore` varchar(80) NOT NULL DEFAULT '',
  `loregroup` int(11) NOT NULL DEFAULT 0,
  `magic` int(11) NOT NULL DEFAULT 0,
  `mana` int(11) NOT NULL DEFAULT 0,
  `manaregen` int(11) NOT NULL DEFAULT 0,
  `enduranceregen` int(11) NOT NULL DEFAULT 0,
  `material` int(11) NOT NULL DEFAULT 0,
  `herosforgemodel` int(11) NOT NULL DEFAULT 0,
  `maxcharges` int(11) NOT NULL DEFAULT 0,
  `mr` int(11) NOT NULL DEFAULT 0,
  `nodrop` int(11) NOT NULL DEFAULT 0,
  `norent` int(11) NOT NULL DEFAULT 0,
  `pendingloreflag` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `pr` int(11) NOT NULL DEFAULT 0,
  `procrate` int(11) NOT NULL DEFAULT 0,
  `races` int(11) NOT NULL DEFAULT 0,
  `range` int(11) NOT NULL DEFAULT 0,
  `reclevel` int(11) NOT NULL DEFAULT 0,
  `recskill` int(11) NOT NULL DEFAULT 0,
  `reqlevel` int(11) NOT NULL DEFAULT 0,
  `sellrate` float NOT NULL DEFAULT 0,
  `shielding` int(11) NOT NULL DEFAULT 0,
  `size` int(11) NOT NULL DEFAULT 0,
  `skillmodtype` int(11) NOT NULL DEFAULT 0,
  `skillmodvalue` int(11) NOT NULL DEFAULT 0,
  `slots` int(11) NOT NULL DEFAULT 0,
  `clickeffect` int(11) NOT NULL DEFAULT 0,
  `spellshield` int(11) NOT NULL DEFAULT 0,
  `strikethrough` int(11) NOT NULL DEFAULT 0,
  `stunresist` int(11) NOT NULL DEFAULT 0,
  `summonedflag` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `tradeskills` int(11) NOT NULL DEFAULT 0,
  `favor` int(11) NOT NULL DEFAULT 0,
  `weight` int(11) NOT NULL DEFAULT 0,
  `UNK012` int(11) NOT NULL DEFAULT 0,
  `UNK013` int(11) NOT NULL DEFAULT 0,
  `benefitflag` int(11) NOT NULL DEFAULT 0,
  `UNK054` int(11) NOT NULL DEFAULT 0,
  `UNK059` int(11) NOT NULL DEFAULT 0,
  `booktype` int(11) NOT NULL DEFAULT 0,
  `recastdelay` int(11) NOT NULL DEFAULT 0,
  `recasttype` int(11) NOT NULL DEFAULT 0,
  `guildfavor` int(11) NOT NULL DEFAULT 0,
  `UNK123` int(11) NOT NULL DEFAULT 0,
  `UNK124` int(11) NOT NULL DEFAULT 0,
  `attuneable` int(11) NOT NULL DEFAULT 0,
  `nopet` int(11) NOT NULL DEFAULT 0,
  `updated` datetime DEFAULT NULL,
  `comment` varchar(255) NOT NULL DEFAULT '',
  `UNK127` int(11) NOT NULL DEFAULT 0,
  `pointtype` int(11) NOT NULL DEFAULT 0,
  `potionbelt` int(11) NOT NULL DEFAULT 0,
  `potionbeltslots` int(11) NOT NULL DEFAULT 0,
  `stacksize` int(11) NOT NULL DEFAULT 0,
  `notransfer` int(11) NOT NULL DEFAULT 0,
  `stackable` int(11) NOT NULL DEFAULT 0,
  `UNK134` varchar(255) NOT NULL DEFAULT '',
  `UNK137` int(11) NOT NULL DEFAULT 0,
  `proceffect` int(11) NOT NULL DEFAULT 0,
  `proctype` int(11) NOT NULL DEFAULT 0,
  `proclevel2` int(11) NOT NULL DEFAULT 0,
  `proclevel` int(11) NOT NULL DEFAULT 0,
  `UNK142` int(11) NOT NULL DEFAULT 0,
  `worneffect` int(11) NOT NULL DEFAULT 0,
  `worntype` int(11) NOT NULL DEFAULT 0,
  `wornlevel2` int(11) NOT NULL DEFAULT 0,
  `wornlevel` int(11) NOT NULL DEFAULT 0,
  `UNK147` int(11) NOT NULL DEFAULT 0,
  `focustype` int(11) NOT NULL DEFAULT 0,
  `focuslevel2` int(11) NOT NULL DEFAULT 0,
  `focuslevel` int(11) NOT NULL DEFAULT 0,
  `UNK152` int(11) NOT NULL DEFAULT 0,
  `scrolleffect` int(11) NOT NULL DEFAULT 0,
  `scrolltype` int(11) NOT NULL DEFAULT 0,
  `scrolllevel2` int(11) NOT NULL DEFAULT 0,
  `scrolllevel` int(11) NOT NULL DEFAULT 0,
  `UNK157` int(11) NOT NULL DEFAULT 0,
  `serialized` datetime DEFAULT NULL,
  `verified` datetime DEFAULT NULL,
  `serialization` text DEFAULT NULL,
  `source` varchar(20) NOT NULL DEFAULT '',
  `UNK033` int(11) NOT NULL DEFAULT 0,
  `lorefile` varchar(32) NOT NULL DEFAULT '',
  `UNK014` int(11) NOT NULL DEFAULT 0,
  `svcorruption` int(11) NOT NULL DEFAULT 0,
  `skillmodmax` int(11) NOT NULL DEFAULT 0,
  `UNK060` int(11) NOT NULL DEFAULT 0,
  `augslot1unk2` int(11) NOT NULL DEFAULT 0,
  `augslot2unk2` int(11) NOT NULL DEFAULT 0,
  `augslot3unk2` int(11) NOT NULL DEFAULT 0,
  `augslot4unk2` int(11) NOT NULL DEFAULT 0,
  `augslot5unk2` int(11) NOT NULL DEFAULT 0,
  `augslot6unk2` int(11) NOT NULL DEFAULT 0,
  `UNK120` int(11) NOT NULL DEFAULT 0,
  `UNK121` int(11) NOT NULL DEFAULT 0,
  `questitemflag` int(11) NOT NULL DEFAULT 0,
  `UNK132` text DEFAULT NULL,
  `clickunk5` int(11) NOT NULL DEFAULT 0,
  `clickunk6` varchar(32) NOT NULL DEFAULT '',
  `clickunk7` int(11) NOT NULL DEFAULT 0,
  `procunk1` int(11) NOT NULL DEFAULT 0,
  `procunk2` int(11) NOT NULL DEFAULT 0,
  `procunk3` int(11) NOT NULL DEFAULT 0,
  `procunk4` int(11) NOT NULL DEFAULT 0,
  `procunk6` varchar(32) NOT NULL DEFAULT '',
  `procunk7` int(11) NOT NULL DEFAULT 0,
  `wornunk1` int(11) NOT NULL DEFAULT 0,
  `wornunk2` int(11) NOT NULL DEFAULT 0,
  `wornunk3` int(11) NOT NULL DEFAULT 0,
  `wornunk4` int(11) NOT NULL DEFAULT 0,
  `wornunk5` int(11) NOT NULL DEFAULT 0,
  `wornunk6` varchar(32) NOT NULL DEFAULT '',
  `wornunk7` int(11) NOT NULL DEFAULT 0,
  `focusunk1` int(11) NOT NULL DEFAULT 0,
  `focusunk2` int(11) NOT NULL DEFAULT 0,
  `focusunk3` int(11) NOT NULL DEFAULT 0,
  `focusunk4` int(11) NOT NULL DEFAULT 0,
  `focusunk5` int(11) NOT NULL DEFAULT 0,
  `focusunk6` varchar(32) NOT NULL DEFAULT '',
  `focusunk7` int(11) NOT NULL DEFAULT 0,
  `scrollunk1` int(11) NOT NULL DEFAULT 0,
  `scrollunk2` int(11) NOT NULL DEFAULT 0,
  `scrollunk3` int(11) NOT NULL DEFAULT 0,
  `scrollunk4` int(11) NOT NULL DEFAULT 0,
  `scrollunk5` int(11) NOT NULL DEFAULT 0,
  `scrollunk6` varchar(32) NOT NULL DEFAULT '',
  `scrollunk7` int(11) NOT NULL DEFAULT 0,
  `UNK193` int(11) NOT NULL DEFAULT 0,
  `purity` int(11) NOT NULL DEFAULT 0,
  `evoitem` int(11) NOT NULL DEFAULT 0,
  `evoid` int(11) NOT NULL DEFAULT 0,
  `evolvinglevel` int(11) NOT NULL DEFAULT 0,
  `evomax` int(11) NOT NULL DEFAULT 0,
  `clickname` varchar(64) NOT NULL DEFAULT '',
  `procname` varchar(64) NOT NULL DEFAULT '',
  `wornname` varchar(64) NOT NULL DEFAULT '',
  `focusname` varchar(64) NOT NULL DEFAULT '',
  `scrollname` varchar(64) NOT NULL DEFAULT '',
  `dsmitigation` smallint(6) NOT NULL DEFAULT 0,
  `heroic_str` smallint(6) NOT NULL DEFAULT 0,
  `heroic_int` smallint(6) NOT NULL DEFAULT 0,
  `heroic_wis` smallint(6) NOT NULL DEFAULT 0,
  `heroic_agi` smallint(6) NOT NULL DEFAULT 0,
  `heroic_dex` smallint(6) NOT NULL DEFAULT 0,
  `heroic_sta` smallint(6) NOT NULL DEFAULT 0,
  `heroic_cha` smallint(6) NOT NULL DEFAULT 0,
  `heroic_pr` smallint(6) NOT NULL DEFAULT 0,
  `heroic_dr` smallint(6) NOT NULL DEFAULT 0,
  `heroic_fr` smallint(6) NOT NULL DEFAULT 0,
  `heroic_cr` smallint(6) NOT NULL DEFAULT 0,
  `heroic_mr` smallint(6) NOT NULL DEFAULT 0,
  `heroic_svcorrup` smallint(6) NOT NULL DEFAULT 0,
  `healamt` smallint(6) NOT NULL DEFAULT 0,
  `spelldmg` smallint(6) NOT NULL DEFAULT 0,
  `clairvoyance` smallint(6) NOT NULL DEFAULT 0,
  `backstabdmg` smallint(6) NOT NULL DEFAULT 0,
  `created` varchar(64) NOT NULL DEFAULT '',
  `elitematerial` smallint(6) NOT NULL DEFAULT 0,
  `ldonsellbackrate` smallint(6) NOT NULL DEFAULT 0,
  `scriptfileid` smallint(6) NOT NULL DEFAULT 0,
  `expendablearrow` smallint(6) NOT NULL DEFAULT 0,
  `powersourcecapacity` smallint(6) NOT NULL DEFAULT 0,
  `bardeffect` smallint(6) NOT NULL DEFAULT 0,
  `bardeffecttype` smallint(6) NOT NULL DEFAULT 0,
  `bardlevel2` smallint(6) NOT NULL DEFAULT 0,
  `bardlevel` smallint(6) NOT NULL DEFAULT 0,
  `bardunk1` smallint(6) NOT NULL DEFAULT 0,
  `bardunk2` smallint(6) NOT NULL DEFAULT 0,
  `bardunk3` smallint(6) NOT NULL DEFAULT 0,
  `bardunk4` smallint(6) NOT NULL DEFAULT 0,
  `bardunk5` smallint(6) NOT NULL DEFAULT 0,
  `bardname` varchar(64) NOT NULL DEFAULT '',
  `bardunk7` smallint(6) NOT NULL DEFAULT 0,
  `UNK214` smallint(6) NOT NULL DEFAULT 0,
  `subtype` int(11) NOT NULL DEFAULT 0,
  `UNK220` int(11) NOT NULL DEFAULT 0,
  `UNK221` int(11) NOT NULL DEFAULT 0,
  `heirloom` int(11) NOT NULL DEFAULT 0,
  `UNK223` int(11) NOT NULL DEFAULT 0,
  `UNK224` int(11) NOT NULL DEFAULT 0,
  `UNK225` int(11) NOT NULL DEFAULT 0,
  `UNK226` int(11) NOT NULL DEFAULT 0,
  `UNK227` int(11) NOT NULL DEFAULT 0,
  `UNK228` int(11) NOT NULL DEFAULT 0,
  `UNK229` int(11) NOT NULL DEFAULT 0,
  `UNK230` int(11) NOT NULL DEFAULT 0,
  `UNK231` int(11) NOT NULL DEFAULT 0,
  `UNK232` int(11) NOT NULL DEFAULT 0,
  `UNK233` int(11) NOT NULL DEFAULT 0,
  `UNK234` int(11) NOT NULL DEFAULT 0,
  `placeable` int(11) NOT NULL DEFAULT 0,
  `UNK236` int(11) NOT NULL DEFAULT 0,
  `UNK237` int(11) NOT NULL DEFAULT 0,
  `UNK238` int(11) NOT NULL DEFAULT 0,
  `UNK239` int(11) NOT NULL DEFAULT 0,
  `UNK240` int(11) NOT NULL DEFAULT 0,
  `UNK241` int(11) NOT NULL DEFAULT 0,
  `epicitem` int(11) NOT NULL DEFAULT 0,
  UNIQUE KEY `ID` (`id`),
  KEY `name_idx` (`Name`),
  KEY `lore_idx` (`lore`),
  KEY `minstatus` (`minstatus`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `keyring`
--

DROP TABLE IF EXISTS `keyring`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `keyring` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `char_id` int(11) NOT NULL DEFAULT 0,
  `item_id` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=38 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `launcher`
--

DROP TABLE IF EXISTS `launcher`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `launcher` (
  `name` varchar(64) NOT NULL DEFAULT '',
  `dynamics` tinyint(3) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `launcher_zones`
--

DROP TABLE IF EXISTS `launcher_zones`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `launcher_zones` (
  `launcher` varchar(64) NOT NULL DEFAULT '',
  `zone` varchar(16) NOT NULL DEFAULT '',
  `port` mediumint(9) NOT NULL DEFAULT 0,
  `date` int(32) NOT NULL,
  PRIMARY KEY (`launcher`,`zone`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `ldon_trap_entries`
--

DROP TABLE IF EXISTS `ldon_trap_entries`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `ldon_trap_entries` (
  `id` int(10) unsigned NOT NULL DEFAULT 0,
  `trap_id` int(10) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`,`trap_id`),
  KEY `id` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `ldon_trap_templates`
--

DROP TABLE IF EXISTS `ldon_trap_templates`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `ldon_trap_templates` (
  `id` int(10) unsigned NOT NULL DEFAULT 0,
  `type` tinyint(3) unsigned NOT NULL DEFAULT 1,
  `spell_id` smallint(5) unsigned NOT NULL DEFAULT 0,
  `skill` smallint(5) unsigned NOT NULL DEFAULT 0,
  `locked` tinyint(3) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `id` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `level_exp_mods`
--

DROP TABLE IF EXISTS `level_exp_mods`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `level_exp_mods` (
  `level` int(11) NOT NULL DEFAULT 0,
  `exp_mod` float DEFAULT NULL,
  `aa_exp_mod` float DEFAULT NULL,
  PRIMARY KEY (`level`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `lfguild`
--

DROP TABLE IF EXISTS `lfguild`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `lfguild` (
  `type` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `name` varchar(32) NOT NULL,
  `comment` varchar(256) NOT NULL,
  `fromlevel` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `tolevel` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `classes` int(10) unsigned NOT NULL DEFAULT 0,
  `aacount` int(10) unsigned NOT NULL DEFAULT 0,
  `timezone` int(10) unsigned NOT NULL DEFAULT 0,
  `timeposted` int(10) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`type`,`name`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `login_accounts`
--

DROP TABLE IF EXISTS `login_accounts`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `login_accounts` (
  `id` int(11) unsigned NOT NULL,
  `account_name` varchar(50) NOT NULL,
  `account_password` text NOT NULL,
  `account_email` varchar(100) NOT NULL,
  `source_loginserver` varchar(64) DEFAULT NULL,
  `last_ip_address` varchar(80) NOT NULL,
  `last_login_date` datetime NOT NULL,
  `created_at` datetime DEFAULT NULL,
  `updated_at` datetime DEFAULT current_timestamp(),
  PRIMARY KEY (`id`),
  UNIQUE KEY `source_loginserver_account_name` (`source_loginserver`,`account_name`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `login_api_tokens`
--

DROP TABLE IF EXISTS `login_api_tokens`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `login_api_tokens` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `token` varchar(200) DEFAULT NULL,
  `can_write` int(11) DEFAULT 0,
  `can_read` int(11) DEFAULT 0,
  `created_at` datetime DEFAULT NULL,
  `updated_at` datetime DEFAULT current_timestamp(),
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `login_authchange`
--

DROP TABLE IF EXISTS `login_authchange`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `login_authchange` (
  `account_id` int(11) unsigned NOT NULL DEFAULT 0,
  `ip` varchar(16) NOT NULL DEFAULT '',
  `time` timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  PRIMARY KEY (`account_id`),
  UNIQUE KEY `ip` (`ip`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `login_server_admins`
--

DROP TABLE IF EXISTS `login_server_admins`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `login_server_admins` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `account_name` varchar(30) NOT NULL,
  `account_password` varchar(255) NOT NULL,
  `first_name` varchar(50) NOT NULL,
  `last_name` varchar(50) NOT NULL,
  `email` varchar(100) NOT NULL,
  `registration_date` datetime NOT NULL,
  `registration_ip_address` varchar(80) NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `login_server_list_types`
--

DROP TABLE IF EXISTS `login_server_list_types`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `login_server_list_types` (
  `id` int(10) unsigned NOT NULL,
  `description` varchar(60) NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `login_world_servers`
--

DROP TABLE IF EXISTS `login_world_servers`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `login_world_servers` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `long_name` varchar(100) NOT NULL,
  `short_name` varchar(100) NOT NULL,
  `tag_description` varchar(50) NOT NULL DEFAULT '',
  `login_server_list_type_id` int(11) NOT NULL,
  `last_login_date` datetime DEFAULT NULL,
  `last_ip_address` varchar(80) DEFAULT NULL,
  `login_server_admin_id` int(11) NOT NULL,
  `is_server_trusted` int(11) NOT NULL,
  `note` varchar(255) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `login_worldservers`
--

DROP TABLE IF EXISTS `login_worldservers`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `login_worldservers` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `account` varchar(30) NOT NULL DEFAULT '',
  `password` varchar(30) NOT NULL DEFAULT '',
  `name` varchar(250) NOT NULL DEFAULT '',
  `admin_id` int(11) unsigned NOT NULL DEFAULT 0,
  `greenname` tinyint(1) unsigned NOT NULL DEFAULT 0,
  `showdown` tinyint(4) NOT NULL DEFAULT 0,
  `chat` tinyint(4) NOT NULL DEFAULT 0,
  `note` tinytext DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `account` (`account`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `loginserver_server_accounts`
--

DROP TABLE IF EXISTS `loginserver_server_accounts`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `loginserver_server_accounts` (
  `LoginServerID` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `AccountName` varchar(30) NOT NULL,
  `AccountPassword` varchar(50) NOT NULL,
  `AccountCreateDate` timestamp NOT NULL DEFAULT current_timestamp(),
  `AccountEmail` varchar(100) NOT NULL,
  `LastLoginDate` datetime NOT NULL,
  `LastIPAddress` varchar(15) NOT NULL,
  PRIMARY KEY (`LoginServerID`,`AccountName`)
) ENGINE=InnoDB AUTO_INCREMENT=17 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `loginserver_server_admin_registration`
--

DROP TABLE IF EXISTS `loginserver_server_admin_registration`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `loginserver_server_admin_registration` (
  `ServerAdminID` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `AccountName` varchar(30) NOT NULL,
  `AccountPassword` varchar(30) NOT NULL,
  `FirstName` varchar(40) NOT NULL,
  `LastName` varchar(50) NOT NULL,
  `Email` varchar(100) NOT NULL DEFAULT '',
  `RegistrationDate` datetime NOT NULL,
  `RegistrationIPAddr` varchar(15) NOT NULL,
  PRIMARY KEY (`ServerAdminID`,`Email`)
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `loginserver_server_list_type`
--

DROP TABLE IF EXISTS `loginserver_server_list_type`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `loginserver_server_list_type` (
  `ServerListTypeID` int(10) unsigned NOT NULL,
  `ServerListTypeDescription` varchar(20) NOT NULL,
  PRIMARY KEY (`ServerListTypeID`)
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `loginserver_world_server_registration`
--

DROP TABLE IF EXISTS `loginserver_world_server_registration`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `loginserver_world_server_registration` (
  `ServerID` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `ServerLongName` varchar(100) NOT NULL,
  `ServerTagDescription` varchar(50) NOT NULL DEFAULT '',
  `ServerShortName` varchar(25) NOT NULL,
  `ServerListTypeID` int(11) NOT NULL,
  `ServerLastLoginDate` datetime DEFAULT NULL,
  `ServerLastIPAddr` varchar(15) DEFAULT NULL,
  `ServerAdminID` int(11) NOT NULL,
  `ServerTrusted` int(11) NOT NULL,
  `Note` varchar(300) DEFAULT NULL,
  PRIMARY KEY (`ServerID`,`ServerLongName`)
) ENGINE=InnoDB AUTO_INCREMENT=1230 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `logs`
--

DROP TABLE IF EXISTS `logs`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `logs` (
  `zone` varchar(16) NOT NULL DEFAULT '',
  `name` varchar(128) NOT NULL DEFAULT '',
  `type` int(10) unsigned NOT NULL DEFAULT 0,
  KEY `zone` (`zone`),
  KEY `name` (`name`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `logsys_categories`
--

DROP TABLE IF EXISTS `logsys_categories`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `logsys_categories` (
  `log_category_id` int(11) NOT NULL,
  `log_category_description` varchar(150) DEFAULT NULL,
  `log_to_console` smallint(11) DEFAULT 0,
  `log_to_file` smallint(11) DEFAULT 0,
  `log_to_gmsay` smallint(11) DEFAULT 0,
  `log_to_discord` smallint(11) DEFAULT 0,
  `discord_webhook_id` int(11) DEFAULT 0,
  PRIMARY KEY (`log_category_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `lootdrop`
--

DROP TABLE IF EXISTS `lootdrop`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `lootdrop` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `name` varchar(255) NOT NULL DEFAULT '',
  `min_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `max_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `content_flags` varchar(100) DEFAULT NULL,
  `content_flags_disabled` varchar(100) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=89452 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `lootdrop_entries`
--

DROP TABLE IF EXISTS `lootdrop_entries`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `lootdrop_entries` (
  `lootdrop_id` int(11) unsigned NOT NULL DEFAULT 0,
  `item_id` int(11) NOT NULL DEFAULT 0,
  `item_charges` smallint(2) unsigned NOT NULL DEFAULT 1,
  `equip_item` tinyint(2) unsigned NOT NULL DEFAULT 0,
  `chance` float NOT NULL DEFAULT 1,
  `disabled_chance` float NOT NULL DEFAULT 0,
  `trivial_min_level` smallint(5) unsigned NOT NULL DEFAULT 0,
  `trivial_max_level` smallint(5) unsigned NOT NULL DEFAULT 0,
  `multiplier` tinyint(2) unsigned NOT NULL DEFAULT 1,
  `npc_min_level` smallint(5) unsigned NOT NULL DEFAULT 0,
  `npc_max_level` smallint(5) unsigned NOT NULL DEFAULT 0,
  `min_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `max_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `content_flags` varchar(100) DEFAULT NULL,
  `content_flags_disabled` varchar(100) DEFAULT NULL,
  PRIMARY KEY (`lootdrop_id`,`item_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `loottable`
--

DROP TABLE IF EXISTS `loottable`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `loottable` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `name` varchar(255) NOT NULL DEFAULT '',
  `mincash` int(11) unsigned NOT NULL DEFAULT 0,
  `maxcash` int(11) unsigned NOT NULL DEFAULT 0,
  `avgcoin` int(10) unsigned NOT NULL DEFAULT 0,
  `done` tinyint(3) NOT NULL DEFAULT 0,
  `min_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `max_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `content_flags` varchar(100) DEFAULT NULL,
  `content_flags_disabled` varchar(100) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=88435 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `loottable_entries`
--

DROP TABLE IF EXISTS `loottable_entries`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `loottable_entries` (
  `loottable_id` int(11) unsigned NOT NULL DEFAULT 0,
  `lootdrop_id` int(11) unsigned NOT NULL DEFAULT 0,
  `multiplier` tinyint(2) unsigned NOT NULL DEFAULT 1,
  `droplimit` tinyint(2) unsigned NOT NULL DEFAULT 1,
  `mindrop` tinyint(2) unsigned NOT NULL DEFAULT 1,
  `probability` float NOT NULL DEFAULT 100,
  PRIMARY KEY (`loottable_id`,`lootdrop_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `mail`
--

DROP TABLE IF EXISTS `mail`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `mail` (
  `msgid` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `charid` int(10) unsigned NOT NULL DEFAULT 0,
  `timestamp` int(11) NOT NULL DEFAULT 0,
  `from` varchar(100) NOT NULL DEFAULT '',
  `subject` varchar(200) NOT NULL DEFAULT '',
  `body` text NOT NULL,
  `to` text NOT NULL,
  `status` tinyint(4) NOT NULL DEFAULT 0,
  PRIMARY KEY (`msgid`),
  KEY `charid` (`charid`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `merc_armorinfo`
--

DROP TABLE IF EXISTS `merc_armorinfo`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `merc_armorinfo` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `merc_npc_type_id` int(11) unsigned NOT NULL,
  `minlevel` tinyint(2) unsigned NOT NULL DEFAULT 1,
  `maxlevel` tinyint(2) unsigned NOT NULL DEFAULT 255,
  `texture` tinyint(2) unsigned NOT NULL DEFAULT 0,
  `helmtexture` tinyint(2) unsigned NOT NULL DEFAULT 0,
  `armortint_id` int(10) unsigned NOT NULL DEFAULT 0,
  `armortint_red` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `armortint_green` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `armortint_blue` tinyint(3) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `merc_buffs`
--

DROP TABLE IF EXISTS `merc_buffs`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `merc_buffs` (
  `MercBuffId` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `MercId` int(10) unsigned NOT NULL DEFAULT 0,
  `SpellId` int(10) unsigned NOT NULL DEFAULT 0,
  `CasterLevel` int(10) unsigned NOT NULL DEFAULT 0,
  `DurationFormula` int(10) unsigned NOT NULL DEFAULT 0,
  `TicsRemaining` int(11) NOT NULL DEFAULT 0,
  `PoisonCounters` int(11) unsigned NOT NULL DEFAULT 0,
  `DiseaseCounters` int(11) unsigned NOT NULL DEFAULT 0,
  `CurseCounters` int(11) unsigned NOT NULL DEFAULT 0,
  `CorruptionCounters` int(11) unsigned NOT NULL DEFAULT 0,
  `HitCount` int(10) unsigned NOT NULL DEFAULT 0,
  `MeleeRune` int(10) unsigned NOT NULL DEFAULT 0,
  `MagicRune` int(10) unsigned NOT NULL DEFAULT 0,
  `dot_rune` int(10) NOT NULL DEFAULT 0,
  `caston_x` int(10) NOT NULL DEFAULT 0,
  `Persistent` tinyint(1) NOT NULL DEFAULT 0,
  `caston_y` int(10) NOT NULL DEFAULT 0,
  `caston_z` int(10) NOT NULL DEFAULT 0,
  `ExtraDIChance` int(10) NOT NULL DEFAULT 0,
  PRIMARY KEY (`MercBuffId`),
  KEY `FK_mercbuff_1` (`MercId`),
  CONSTRAINT `FK_mercbuff_1` FOREIGN KEY (`MercId`) REFERENCES `mercs` (`MercID`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `merc_spell_list_entries`
--

DROP TABLE IF EXISTS `merc_spell_list_entries`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `merc_spell_list_entries` (
  `merc_spell_list_entry_id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `merc_spell_list_id` int(10) unsigned NOT NULL,
  `spell_id` int(10) unsigned NOT NULL,
  `spell_type` int(10) unsigned NOT NULL DEFAULT 0,
  `stance_id` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `minlevel` tinyint(3) unsigned NOT NULL DEFAULT 1,
  `maxlevel` tinyint(3) unsigned NOT NULL DEFAULT 255,
  `slot` tinyint(4) NOT NULL DEFAULT -1,
  `procChance` tinyint(3) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`merc_spell_list_entry_id`),
  KEY `FK_merc_spell_lists_1` (`merc_spell_list_id`),
  CONSTRAINT `FK_merc_spell_lists_1` FOREIGN KEY (`merc_spell_list_id`) REFERENCES `merc_spell_lists` (`merc_spell_list_id`)
) ENGINE=InnoDB AUTO_INCREMENT=293 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `merc_spell_lists`
--

DROP TABLE IF EXISTS `merc_spell_lists`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `merc_spell_lists` (
  `merc_spell_list_id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `class_id` int(10) unsigned NOT NULL,
  `proficiency_id` tinyint(3) unsigned NOT NULL,
  `name` varchar(50) NOT NULL,
  PRIMARY KEY (`merc_spell_list_id`)
) ENGINE=InnoDB AUTO_INCREMENT=9 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `merc_stats`
--

DROP TABLE IF EXISTS `merc_stats`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `merc_stats` (
  `merc_npc_type_id` int(11) unsigned NOT NULL,
  `clientlevel` tinyint(2) unsigned NOT NULL DEFAULT 1,
  `level` tinyint(2) unsigned NOT NULL DEFAULT 1,
  `hp` int(11) NOT NULL DEFAULT 1,
  `mana` int(11) NOT NULL DEFAULT 0,
  `AC` smallint(5) NOT NULL DEFAULT 1,
  `ATK` mediumint(9) NOT NULL DEFAULT 1,
  `STR` mediumint(8) unsigned NOT NULL DEFAULT 75,
  `STA` mediumint(8) unsigned NOT NULL DEFAULT 75,
  `DEX` mediumint(8) unsigned NOT NULL DEFAULT 75,
  `AGI` mediumint(8) unsigned NOT NULL DEFAULT 75,
  `_INT` mediumint(8) unsigned NOT NULL DEFAULT 80,
  `WIS` mediumint(8) unsigned NOT NULL DEFAULT 80,
  `CHA` mediumint(8) unsigned NOT NULL DEFAULT 75,
  `MR` smallint(5) NOT NULL DEFAULT 15,
  `CR` smallint(5) NOT NULL DEFAULT 15,
  `DR` smallint(5) NOT NULL DEFAULT 15,
  `FR` smallint(5) NOT NULL DEFAULT 15,
  `PR` smallint(5) NOT NULL DEFAULT 15,
  `Corrup` smallint(5) NOT NULL DEFAULT 15,
  `mindmg` int(10) unsigned NOT NULL DEFAULT 1,
  `maxdmg` int(10) unsigned NOT NULL DEFAULT 1,
  `attack_count` smallint(6) NOT NULL DEFAULT 0,
  `attack_speed` tinyint(3) NOT NULL DEFAULT 0,
  `attack_delay` tinyint(3) unsigned NOT NULL DEFAULT 30,
  `special_abilities` text DEFAULT NULL,
  `Accuracy` mediumint(9) NOT NULL DEFAULT 0,
  `hp_regen_rate` int(11) unsigned NOT NULL DEFAULT 1,
  `mana_regen_rate` int(11) unsigned NOT NULL DEFAULT 1,
  `runspeed` float NOT NULL DEFAULT 0,
  `statscale` int(11) NOT NULL DEFAULT 100,
  `spellscale` float NOT NULL DEFAULT 100,
  `healscale` float NOT NULL DEFAULT 100,
  PRIMARY KEY (`merc_npc_type_id`,`clientlevel`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `merc_weaponinfo`
--

DROP TABLE IF EXISTS `merc_weaponinfo`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `merc_weaponinfo` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `merc_npc_type_id` int(11) NOT NULL,
  `minlevel` tinyint(2) unsigned NOT NULL DEFAULT 0,
  `maxlevel` tinyint(2) unsigned NOT NULL DEFAULT 0,
  `d_melee_texture1` int(11) NOT NULL DEFAULT 0,
  `d_melee_texture2` int(11) NOT NULL DEFAULT 0,
  `prim_melee_type` tinyint(4) unsigned NOT NULL DEFAULT 28,
  `sec_melee_type` tinyint(4) unsigned NOT NULL DEFAULT 28,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `merchantlist`
--

DROP TABLE IF EXISTS `merchantlist`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `merchantlist` (
  `merchantid` int(11) NOT NULL DEFAULT 0,
  `slot` int(11) unsigned NOT NULL DEFAULT 0,
  `item` int(11) NOT NULL DEFAULT 0,
  `faction_required` smallint(6) NOT NULL DEFAULT -100,
  `level_required` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `min_status` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `max_status` tinyint(3) unsigned NOT NULL DEFAULT 255,
  `alt_currency_cost` smallint(5) unsigned NOT NULL DEFAULT 0,
  `classes_required` int(11) NOT NULL DEFAULT 65535,
  `probability` int(3) NOT NULL DEFAULT 100,
  `bucket_name` varchar(100) NOT NULL DEFAULT '',
  `bucket_value` varchar(100) NOT NULL DEFAULT '',
  `bucket_comparison` tinyint(3) unsigned DEFAULT 0,
  `min_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `max_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `content_flags` varchar(100) DEFAULT NULL,
  `content_flags_disabled` varchar(100) DEFAULT NULL,
  PRIMARY KEY (`merchantid`,`slot`),
  UNIQUE KEY `merchantid` (`merchantid`,`item`),
  KEY `item` (`item`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `merchantlist_temp`
--

DROP TABLE IF EXISTS `merchantlist_temp`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `merchantlist_temp` (
  `npcid` int(10) unsigned NOT NULL DEFAULT 0,
  `slot` int(11) unsigned NOT NULL DEFAULT 0,
  `zone_id` int(11) NOT NULL DEFAULT 0,
  `instance_id` int(11) NOT NULL DEFAULT 0,
  `itemid` int(10) unsigned NOT NULL DEFAULT 0,
  `charges` int(10) unsigned NOT NULL DEFAULT 1,
  PRIMARY KEY (`npcid`,`slot`,`zone_id`,`instance_id`),
  KEY `npcid_2` (`npcid`,`itemid`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `merchantsets`
--

DROP TABLE IF EXISTS `merchantsets`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `merchantsets` (
  `Id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `Name` varchar(128) NOT NULL DEFAULT '',
  `showname` varchar(31) DEFAULT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `mercs`
--

DROP TABLE IF EXISTS `mercs`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `mercs` (
  `MercID` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `OwnerCharacterID` int(10) unsigned NOT NULL,
  `Slot` tinyint(1) unsigned NOT NULL DEFAULT 0,
  `Name` varchar(64) NOT NULL,
  `TemplateID` int(10) unsigned NOT NULL DEFAULT 0,
  `SuspendedTime` int(11) unsigned NOT NULL DEFAULT 0,
  `IsSuspended` tinyint(1) unsigned NOT NULL DEFAULT 0,
  `TimerRemaining` int(11) unsigned NOT NULL DEFAULT 0,
  `Gender` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `MercSize` float NOT NULL DEFAULT 5,
  `StanceID` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `HP` int(11) unsigned NOT NULL DEFAULT 0,
  `Mana` int(11) unsigned NOT NULL DEFAULT 0,
  `Endurance` int(11) unsigned NOT NULL DEFAULT 0,
  `Face` int(10) unsigned NOT NULL DEFAULT 1,
  `LuclinHairStyle` int(10) unsigned NOT NULL DEFAULT 1,
  `LuclinHairColor` int(10) unsigned NOT NULL DEFAULT 1,
  `LuclinEyeColor` int(10) unsigned NOT NULL DEFAULT 1,
  `LuclinEyeColor2` int(10) unsigned NOT NULL DEFAULT 1,
  `LuclinBeardColor` int(10) unsigned NOT NULL DEFAULT 1,
  `LuclinBeard` int(10) unsigned NOT NULL DEFAULT 0,
  `DrakkinHeritage` int(10) unsigned NOT NULL DEFAULT 0,
  `DrakkinTattoo` int(10) unsigned NOT NULL DEFAULT 0,
  `DrakkinDetails` int(10) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`MercID`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `name_filter`
--

DROP TABLE IF EXISTS `name_filter`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `name_filter` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(30) NOT NULL DEFAULT '',
  PRIMARY KEY (`id`) USING BTREE,
  KEY `name_search_index` (`name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `npc_emotes`
--

DROP TABLE IF EXISTS `npc_emotes`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `npc_emotes` (
  `id` int(10) NOT NULL AUTO_INCREMENT,
  `emoteid` int(10) unsigned NOT NULL DEFAULT 0,
  `event_` tinyint(3) NOT NULL DEFAULT 0,
  `type` tinyint(3) NOT NULL DEFAULT 0,
  `text` varchar(512) NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=2323 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `npc_faction`
--

DROP TABLE IF EXISTS `npc_faction`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `npc_faction` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` tinytext DEFAULT NULL,
  `primaryfaction` int(11) NOT NULL DEFAULT 0,
  `ignore_primary_assist` tinyint(3) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`)
) ENGINE=MyISAM AUTO_INCREMENT=20254 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `npc_faction_entries`
--

DROP TABLE IF EXISTS `npc_faction_entries`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `npc_faction_entries` (
  `npc_faction_id` int(11) unsigned NOT NULL DEFAULT 0,
  `faction_id` int(11) unsigned NOT NULL DEFAULT 0,
  `value` int(11) NOT NULL DEFAULT 0,
  `npc_value` tinyint(3) NOT NULL DEFAULT 0,
  `temp` tinyint(3) NOT NULL DEFAULT 0,
  PRIMARY KEY (`npc_faction_id`,`faction_id`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `npc_faction_entries_prefix`
--

DROP TABLE IF EXISTS `npc_faction_entries_prefix`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `npc_faction_entries_prefix` (
  `npc_faction_id` int(11) unsigned NOT NULL DEFAULT 0,
  `faction_id` int(11) unsigned NOT NULL DEFAULT 0,
  `value` int(11) NOT NULL DEFAULT 0,
  `npc_value` tinyint(3) NOT NULL DEFAULT 0,
  `temp` tinyint(3) NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `npc_faction_prefix`
--

DROP TABLE IF EXISTS `npc_faction_prefix`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `npc_faction_prefix` (
  `id` int(11) NOT NULL DEFAULT 0,
  `name` tinytext CHARACTER SET utf8mb3 COLLATE utf8mb3_uca1400_ai_ci DEFAULT NULL,
  `primaryfaction` int(11) NOT NULL DEFAULT 0,
  `ignore_primary_assist` tinyint(3) NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `npc_scale_global_base`
--

DROP TABLE IF EXISTS `npc_scale_global_base`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `npc_scale_global_base` (
  `type` int(11) NOT NULL DEFAULT 0,
  `level` int(11) NOT NULL,
  `zone_id_list` int(11) unsigned NOT NULL DEFAULT 0,
  `instance_version_list` int(11) unsigned NOT NULL DEFAULT 0,
  `ac` int(11) NOT NULL DEFAULT 0,
  `hp` bigint(20) NOT NULL DEFAULT 0,
  `accuracy` int(11) NOT NULL DEFAULT 0,
  `slow_mitigation` int(11) NOT NULL DEFAULT 0,
  `attack` int(11) NOT NULL DEFAULT 0,
  `strength` int(11) NOT NULL DEFAULT 0,
  `stamina` int(11) NOT NULL DEFAULT 0,
  `dexterity` int(11) NOT NULL DEFAULT 0,
  `agility` int(11) NOT NULL DEFAULT 0,
  `intelligence` int(11) NOT NULL DEFAULT 0,
  `wisdom` int(11) NOT NULL DEFAULT 0,
  `charisma` int(11) NOT NULL DEFAULT 0,
  `magic_resist` int(11) NOT NULL DEFAULT 0,
  `cold_resist` int(11) NOT NULL DEFAULT 0,
  `fire_resist` int(11) NOT NULL DEFAULT 0,
  `poison_resist` int(11) NOT NULL DEFAULT 0,
  `disease_resist` int(11) NOT NULL DEFAULT 0,
  `corruption_resist` int(11) NOT NULL DEFAULT 0,
  `physical_resist` int(11) NOT NULL DEFAULT 0,
  `min_dmg` int(11) NOT NULL DEFAULT 0,
  `max_dmg` int(11) NOT NULL DEFAULT 0,
  `hp_regen_rate` bigint(20) NOT NULL DEFAULT 0,
  `hp_regen_per_second` bigint(20) NOT NULL DEFAULT 0,
  `attack_delay` int(11) NOT NULL DEFAULT 0,
  `spell_scale` int(11) NOT NULL DEFAULT 100,
  `heal_scale` int(11) NOT NULL DEFAULT 100,
  `avoidance` int(11) unsigned NOT NULL DEFAULT 0,
  `heroic_strikethrough` int(11) NOT NULL DEFAULT 0,
  `special_abilities` text CHARACTER SET latin1 COLLATE latin1_swedish_ci NOT NULL,
  PRIMARY KEY (`type`,`level`,`zone_id_list`,`instance_version_list`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `npc_spells`
--

DROP TABLE IF EXISTS `npc_spells`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `npc_spells` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `name` tinytext DEFAULT NULL,
  `parent_list` int(10) unsigned NOT NULL DEFAULT 0,
  `attack_proc` smallint(5) NOT NULL DEFAULT -1,
  `proc_chance` tinyint(3) NOT NULL DEFAULT 3,
  `range_proc` smallint(5) NOT NULL DEFAULT -1,
  `rproc_chance` smallint(5) NOT NULL DEFAULT 0,
  `defensive_proc` smallint(5) NOT NULL DEFAULT -1,
  `dproc_chance` smallint(5) NOT NULL DEFAULT 0,
  `fail_recast` int(11) unsigned NOT NULL DEFAULT 0,
  `engaged_no_sp_recast_min` int(11) unsigned NOT NULL DEFAULT 0,
  `engaged_no_sp_recast_max` int(11) unsigned NOT NULL DEFAULT 0,
  `engaged_b_self_chance` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `engaged_b_other_chance` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `engaged_d_chance` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `pursue_no_sp_recast_min` int(3) unsigned NOT NULL DEFAULT 0,
  `pursue_no_sp_recast_max` int(11) unsigned NOT NULL DEFAULT 0,
  `pursue_d_chance` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `idle_no_sp_recast_min` int(11) unsigned NOT NULL DEFAULT 0,
  `idle_no_sp_recast_max` int(11) unsigned NOT NULL DEFAULT 0,
  `idle_b_chance` tinyint(11) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`)
) ENGINE=MyISAM AUTO_INCREMENT=3108 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `npc_spells_effects`
--

DROP TABLE IF EXISTS `npc_spells_effects`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `npc_spells_effects` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `name` tinytext DEFAULT NULL,
  `parent_list` int(11) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=31 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `npc_spells_effects_entries`
--

DROP TABLE IF EXISTS `npc_spells_effects_entries`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `npc_spells_effects_entries` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `npc_spells_effects_id` int(11) NOT NULL DEFAULT 0,
  `spell_effect_id` smallint(5) NOT NULL DEFAULT 0,
  `minlevel` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `maxlevel` tinyint(3) unsigned NOT NULL DEFAULT 255,
  `se_base` int(11) NOT NULL DEFAULT 0,
  `se_limit` int(11) NOT NULL DEFAULT 0,
  `se_max` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `spellsid_spellid` (`npc_spells_effects_id`,`spell_effect_id`)
) ENGINE=InnoDB AUTO_INCREMENT=31 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `npc_spells_entries`
--

DROP TABLE IF EXISTS `npc_spells_entries`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `npc_spells_entries` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `npc_spells_id` int(11) NOT NULL DEFAULT 0,
  `spellid` smallint(5) unsigned NOT NULL DEFAULT 0,
  `type` smallint(5) unsigned NOT NULL DEFAULT 0,
  `minlevel` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `maxlevel` tinyint(3) unsigned NOT NULL DEFAULT 255,
  `manacost` smallint(5) NOT NULL DEFAULT -1,
  `recast_delay` int(11) NOT NULL DEFAULT -1,
  `priority` smallint(5) NOT NULL DEFAULT 0,
  `resist_adjust` int(11) DEFAULT NULL,
  `min_hp` smallint(5) DEFAULT 0,
  `max_hp` smallint(5) DEFAULT 0,
  `min_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `max_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `content_flags` varchar(100) DEFAULT NULL,
  `content_flags_disabled` varchar(100) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `spellsid_spellid` (`npc_spells_id`,`spellid`)
) ENGINE=MyISAM AUTO_INCREMENT=9444 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `npc_types`
--

DROP TABLE IF EXISTS `npc_types`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `npc_types` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` text NOT NULL,
  `lastname` varchar(32) DEFAULT NULL,
  `level` tinyint(2) unsigned NOT NULL DEFAULT 0,
  `race` smallint(5) unsigned NOT NULL DEFAULT 0,
  `class` tinyint(2) unsigned NOT NULL DEFAULT 0,
  `bodytype` int(11) NOT NULL DEFAULT 1,
  `hp` bigint(20) NOT NULL DEFAULT 0,
  `mana` bigint(20) NOT NULL DEFAULT 0,
  `gender` tinyint(2) unsigned NOT NULL DEFAULT 0,
  `texture` tinyint(2) unsigned NOT NULL DEFAULT 0,
  `helmtexture` tinyint(2) unsigned NOT NULL DEFAULT 0,
  `herosforgemodel` int(11) NOT NULL DEFAULT 0,
  `size` float NOT NULL DEFAULT 0,
  `hp_regen_rate` bigint(20) NOT NULL DEFAULT 0,
  `hp_regen_per_second` bigint(20) NOT NULL DEFAULT 0,
  `mana_regen_rate` bigint(20) NOT NULL DEFAULT 0,
  `loottable_id` int(11) unsigned NOT NULL DEFAULT 0,
  `merchant_id` int(11) unsigned NOT NULL DEFAULT 0,
  `greed` tinyint(8) unsigned NOT NULL DEFAULT 0,
  `alt_currency_id` int(11) unsigned NOT NULL DEFAULT 0,
  `npc_spells_id` int(11) unsigned NOT NULL DEFAULT 0,
  `npc_spells_effects_id` int(11) unsigned NOT NULL DEFAULT 0,
  `npc_faction_id` int(11) NOT NULL DEFAULT 0,
  `adventure_template_id` int(10) unsigned NOT NULL DEFAULT 0,
  `trap_template` int(10) unsigned DEFAULT 0,
  `mindmg` int(10) unsigned NOT NULL DEFAULT 0,
  `maxdmg` int(10) unsigned NOT NULL DEFAULT 0,
  `attack_count` smallint(6) NOT NULL DEFAULT -1,
  `npcspecialattks` varchar(36) NOT NULL DEFAULT '',
  `special_abilities` text CHARACTER SET latin1 COLLATE latin1_swedish_ci DEFAULT NULL,
  `aggroradius` int(10) unsigned NOT NULL DEFAULT 0,
  `assistradius` int(10) unsigned NOT NULL DEFAULT 0,
  `face` int(10) unsigned NOT NULL DEFAULT 1,
  `luclin_hairstyle` int(10) unsigned NOT NULL DEFAULT 1,
  `luclin_haircolor` int(10) unsigned NOT NULL DEFAULT 1,
  `luclin_eyecolor` int(10) unsigned NOT NULL DEFAULT 1,
  `luclin_eyecolor2` int(10) unsigned NOT NULL DEFAULT 1,
  `luclin_beardcolor` int(10) unsigned NOT NULL DEFAULT 1,
  `luclin_beard` int(10) unsigned NOT NULL DEFAULT 0,
  `drakkin_heritage` int(10) NOT NULL DEFAULT 0,
  `drakkin_tattoo` int(10) NOT NULL DEFAULT 0,
  `drakkin_details` int(10) NOT NULL DEFAULT 0,
  `armortint_id` int(10) unsigned NOT NULL DEFAULT 0,
  `armortint_red` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `armortint_green` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `armortint_blue` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `d_melee_texture1` int(11) NOT NULL DEFAULT 0,
  `d_melee_texture2` int(11) NOT NULL DEFAULT 0,
  `ammo_idfile` varchar(30) NOT NULL DEFAULT 'IT10',
  `prim_melee_type` tinyint(4) unsigned NOT NULL DEFAULT 28,
  `sec_melee_type` tinyint(4) unsigned NOT NULL DEFAULT 28,
  `ranged_type` tinyint(4) unsigned NOT NULL DEFAULT 7,
  `runspeed` float NOT NULL DEFAULT 0,
  `MR` smallint(5) NOT NULL DEFAULT 0,
  `CR` smallint(5) NOT NULL DEFAULT 0,
  `DR` smallint(5) NOT NULL DEFAULT 0,
  `FR` smallint(5) NOT NULL DEFAULT 0,
  `PR` smallint(5) NOT NULL DEFAULT 0,
  `Corrup` smallint(5) NOT NULL DEFAULT 0,
  `PhR` smallint(5) unsigned NOT NULL DEFAULT 0,
  `see_invis` smallint(4) NOT NULL DEFAULT 0,
  `see_invis_undead` smallint(4) NOT NULL DEFAULT 0,
  `qglobal` int(2) unsigned NOT NULL DEFAULT 0,
  `AC` smallint(5) NOT NULL DEFAULT 0,
  `npc_aggro` tinyint(4) NOT NULL DEFAULT 0,
  `spawn_limit` tinyint(4) NOT NULL DEFAULT 0,
  `attack_speed` float NOT NULL DEFAULT 0,
  `attack_delay` tinyint(3) unsigned NOT NULL DEFAULT 30,
  `findable` tinyint(4) NOT NULL DEFAULT 0,
  `STR` mediumint(8) unsigned NOT NULL DEFAULT 75,
  `STA` mediumint(8) unsigned NOT NULL DEFAULT 75,
  `DEX` mediumint(8) unsigned NOT NULL DEFAULT 75,
  `AGI` mediumint(8) unsigned NOT NULL DEFAULT 75,
  `_INT` mediumint(8) unsigned NOT NULL DEFAULT 80,
  `WIS` mediumint(8) unsigned NOT NULL DEFAULT 75,
  `CHA` mediumint(8) unsigned NOT NULL DEFAULT 75,
  `see_hide` tinyint(4) NOT NULL DEFAULT 0,
  `see_improved_hide` tinyint(4) NOT NULL DEFAULT 0,
  `trackable` tinyint(4) NOT NULL DEFAULT 1,
  `isbot` tinyint(4) NOT NULL DEFAULT 0,
  `exclude` tinyint(4) NOT NULL DEFAULT 1,
  `ATK` mediumint(9) NOT NULL DEFAULT 0,
  `Accuracy` mediumint(9) NOT NULL DEFAULT 0,
  `Avoidance` mediumint(9) unsigned NOT NULL DEFAULT 0,
  `slow_mitigation` smallint(4) NOT NULL DEFAULT 0,
  `version` smallint(5) unsigned NOT NULL DEFAULT 0,
  `maxlevel` tinyint(3) NOT NULL DEFAULT 0,
  `scalerate` int(11) NOT NULL DEFAULT 100,
  `private_corpse` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `unique_spawn_by_name` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `underwater` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `isquest` tinyint(2) NOT NULL DEFAULT 0,
  `emoteid` int(10) unsigned NOT NULL DEFAULT 0,
  `spellscale` float NOT NULL DEFAULT 100,
  `healscale` float NOT NULL DEFAULT 100,
  `no_target_hotkey` tinyint(2) unsigned NOT NULL DEFAULT 0,
  `raid_target` tinyint(2) unsigned NOT NULL DEFAULT 0,
  `armtexture` tinyint(2) NOT NULL DEFAULT 0,
  `bracertexture` tinyint(2) NOT NULL DEFAULT 0,
  `handtexture` tinyint(2) NOT NULL DEFAULT 0,
  `legtexture` tinyint(2) NOT NULL DEFAULT 0,
  `feettexture` tinyint(2) NOT NULL DEFAULT 0,
  `light` tinyint(2) NOT NULL DEFAULT 0,
  `walkspeed` tinyint(2) NOT NULL DEFAULT 0,
  `peqid` int(10) NOT NULL DEFAULT 0,
  `unique_` tinyint(4) NOT NULL DEFAULT 0,
  `fixed` tinyint(4) NOT NULL DEFAULT 0,
  `ignore_despawn` tinyint(2) NOT NULL DEFAULT 0,
  `show_name` tinyint(2) NOT NULL DEFAULT 1,
  `untargetable` tinyint(2) NOT NULL DEFAULT 0,
  `charm_ac` smallint(5) DEFAULT 0,
  `charm_min_dmg` int(10) DEFAULT 0,
  `charm_max_dmg` int(10) DEFAULT 0,
  `charm_attack_delay` tinyint(3) DEFAULT 0,
  `charm_accuracy_rating` mediumint(9) DEFAULT 0,
  `charm_avoidance_rating` mediumint(9) DEFAULT 0,
  `charm_atk` mediumint(9) DEFAULT 0,
  `skip_global_loot` tinyint(4) DEFAULT 0,
  `rare_spawn` tinyint(4) DEFAULT 0,
  `stuck_behavior` tinyint(4) NOT NULL DEFAULT 0,
  `flymode` tinyint(4) NOT NULL DEFAULT -1,
  `model` smallint(5) NOT NULL DEFAULT 0,
  `always_aggro` tinyint(1) NOT NULL DEFAULT 0,
  `exp_mod` int(11) NOT NULL DEFAULT 100,
  `heroic_strikethrough` int(11) NOT NULL DEFAULT 0,
  `faction_amount` int(10) NOT NULL DEFAULT 0,
  `keeps_sold_items` tinyint(1) unsigned NOT NULL DEFAULT 1,
  `is_parcel_merchant` tinyint(1) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=5000095 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `npc_types_metadata`
--

DROP TABLE IF EXISTS `npc_types_metadata`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `npc_types_metadata` (
  `npc_types_id` int(11) NOT NULL DEFAULT 0,
  `isPKMob` tinyint(4) NOT NULL DEFAULT 0,
  `isNamedMob` tinyint(4) NOT NULL DEFAULT 0,
  `isRaidTarget` tinyint(4) NOT NULL DEFAULT 0,
  `isCreatedMob` tinyint(4) NOT NULL DEFAULT 0,
  `isCustomFeatureNPC` tinyint(4) NOT NULL DEFAULT 0,
  PRIMARY KEY (`npc_types_id`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `npc_types_tint`
--

DROP TABLE IF EXISTS `npc_types_tint`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `npc_types_tint` (
  `id` int(10) unsigned NOT NULL DEFAULT 0,
  `tint_set_name` text NOT NULL,
  `red1h` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `grn1h` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `blu1h` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `red2c` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `grn2c` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `blu2c` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `red3a` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `grn3a` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `blu3a` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `red4b` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `grn4b` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `blu4b` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `red5g` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `grn5g` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `blu5g` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `red6l` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `grn6l` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `blu6l` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `red7f` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `grn7f` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `blu7f` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `red8x` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `grn8x` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `blu8x` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `red9x` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `grn9x` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `blu9x` tinyint(3) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `object`
--

DROP TABLE IF EXISTS `object`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `object` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `zoneid` int(11) unsigned NOT NULL DEFAULT 0,
  `version` smallint(5) NOT NULL DEFAULT 0,
  `xpos` float NOT NULL DEFAULT 0,
  `ypos` float NOT NULL DEFAULT 0,
  `zpos` float NOT NULL DEFAULT 0,
  `heading` float NOT NULL DEFAULT 0,
  `itemid` int(11) NOT NULL DEFAULT 0,
  `charges` smallint(3) unsigned NOT NULL DEFAULT 0,
  `objectname` varchar(32) DEFAULT NULL,
  `type` int(11) NOT NULL DEFAULT 0,
  `icon` int(11) NOT NULL DEFAULT 0,
  `size_percentage` float NOT NULL DEFAULT 0,
  `unknown24` int(11) NOT NULL DEFAULT 0,
  `unknown60` int(11) NOT NULL DEFAULT 0,
  `unknown64` int(11) NOT NULL DEFAULT 0,
  `unknown68` int(11) NOT NULL DEFAULT 0,
  `unknown72` int(11) NOT NULL DEFAULT 0,
  `unknown76` int(11) NOT NULL DEFAULT 0,
  `unknown84` int(11) NOT NULL DEFAULT 0,
  `size` float NOT NULL DEFAULT 100,
  `solid_type` mediumint(5) NOT NULL DEFAULT 0,
  `incline` int(11) NOT NULL DEFAULT 0,
  `tilt_x` float NOT NULL DEFAULT 0,
  `tilt_y` float NOT NULL DEFAULT 0,
  `display_name` varchar(64) DEFAULT NULL,
  `min_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `max_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `content_flags` varchar(100) DEFAULT NULL,
  `content_flags_disabled` varchar(100) DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `zone` (`zoneid`)
) ENGINE=MyISAM AUTO_INCREMENT=536 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `object_contents`
--

DROP TABLE IF EXISTS `object_contents`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `object_contents` (
  `zoneid` int(11) unsigned NOT NULL DEFAULT 0,
  `parentid` int(11) unsigned NOT NULL DEFAULT 0,
  `bagidx` int(11) unsigned NOT NULL DEFAULT 0,
  `itemid` int(11) unsigned NOT NULL DEFAULT 0,
  `charges` smallint(3) NOT NULL DEFAULT 0,
  `droptime` datetime NOT NULL DEFAULT current_timestamp(),
  `augslot1` mediumint(7) unsigned DEFAULT 0,
  `augslot2` mediumint(7) unsigned DEFAULT 0,
  `augslot3` mediumint(7) unsigned DEFAULT 0,
  `augslot4` mediumint(7) unsigned DEFAULT 0,
  `augslot5` mediumint(7) unsigned DEFAULT 0,
  `augslot6` mediumint(7) NOT NULL DEFAULT 0,
  PRIMARY KEY (`parentid`,`bagidx`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `peq_admin`
--

DROP TABLE IF EXISTS `peq_admin`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `peq_admin` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `login` varchar(30) NOT NULL,
  `password` varchar(255) NOT NULL,
  `administrator` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `perl_event_export_settings`
--

DROP TABLE IF EXISTS `perl_event_export_settings`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `perl_event_export_settings` (
  `event_id` int(11) NOT NULL,
  `event_description` varchar(150) DEFAULT NULL,
  `export_qglobals` smallint(11) DEFAULT 0,
  `export_mob` smallint(11) DEFAULT 0,
  `export_zone` smallint(11) DEFAULT 0,
  `export_item` smallint(11) DEFAULT 0,
  `export_event` smallint(11) DEFAULT 0,
  PRIMARY KEY (`event_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `petitions`
--

DROP TABLE IF EXISTS `petitions`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `petitions` (
  `dib` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `petid` int(10) unsigned NOT NULL DEFAULT 0,
  `charname` varchar(32) NOT NULL DEFAULT '',
  `accountname` varchar(32) NOT NULL DEFAULT '',
  `lastgm` varchar(32) NOT NULL DEFAULT '',
  `petitiontext` text NOT NULL,
  `gmtext` text DEFAULT NULL,
  `zone` varchar(32) NOT NULL DEFAULT '',
  `urgency` int(11) NOT NULL DEFAULT 0,
  `charclass` int(11) NOT NULL DEFAULT 0,
  `charrace` int(11) NOT NULL DEFAULT 0,
  `charlevel` int(11) NOT NULL DEFAULT 0,
  `checkouts` int(11) NOT NULL DEFAULT 0,
  `unavailables` int(11) NOT NULL DEFAULT 0,
  `ischeckedout` tinyint(4) NOT NULL DEFAULT 0,
  `senttime` bigint(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`dib`),
  KEY `petid` (`petid`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `pets`
--

DROP TABLE IF EXISTS `pets`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `pets` (
  `id` int(20) NOT NULL AUTO_INCREMENT,
  `type` varchar(64) NOT NULL DEFAULT '',
  `petpower` int(11) NOT NULL DEFAULT 0,
  `npcID` int(11) NOT NULL DEFAULT 0,
  `temp` tinyint(4) NOT NULL DEFAULT 0,
  `petcontrol` tinyint(4) NOT NULL DEFAULT 0,
  `petnaming` tinyint(4) NOT NULL DEFAULT 0,
  `monsterflag` tinyint(4) NOT NULL DEFAULT 0,
  `equipmentset` int(11) NOT NULL DEFAULT -1,
  PRIMARY KEY (`id`),
  UNIQUE KEY `type_petpower` (`type`,`petpower`)
) ENGINE=InnoDB AUTO_INCREMENT=257 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `pets__`
--

DROP TABLE IF EXISTS `pets__`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `pets__` (
  `type` varchar(64) NOT NULL DEFAULT '',
  `npcID` int(11) NOT NULL DEFAULT 0,
  `temp` tinyint(4) NOT NULL DEFAULT 0,
  PRIMARY KEY (`type`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `pets_beastlord_data`
--

DROP TABLE IF EXISTS `pets_beastlord_data`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `pets_beastlord_data` (
  `player_race` int(10) unsigned NOT NULL DEFAULT 1,
  `pet_race` int(10) unsigned NOT NULL DEFAULT 42,
  `texture` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `helm_texture` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `gender` tinyint(3) unsigned NOT NULL DEFAULT 2,
  `size_modifier` float unsigned DEFAULT 1,
  `face` tinyint(3) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`player_race`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci ROW_FORMAT=COMPACT;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `player_corpses_backup`
--

DROP TABLE IF EXISTS `player_corpses_backup`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `player_corpses_backup` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `charid` int(10) unsigned NOT NULL DEFAULT 0,
  `parent_corpse_id` int(10) unsigned NOT NULL DEFAULT 0,
  `zoneid` smallint(11) NOT NULL DEFAULT 0,
  `x` float NOT NULL DEFAULT 0,
  `y` float NOT NULL DEFAULT 0,
  `z` float NOT NULL DEFAULT 0,
  `heading` float NOT NULL DEFAULT 0,
  `data` blob DEFAULT NULL,
  `timeofdeath` datetime NOT NULL DEFAULT '0000-00-00 00:00:00',
  `timeofdelete` datetime NOT NULL DEFAULT '0000-00-00 00:00:00',
  PRIMARY KEY (`id`),
  KEY `charid` (`charid`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `player_event_log_settings`
--

DROP TABLE IF EXISTS `player_event_log_settings`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `player_event_log_settings` (
  `id` bigint(20) NOT NULL,
  `event_name` varchar(100) DEFAULT NULL,
  `event_enabled` tinyint(1) DEFAULT NULL,
  `retention_days` int(11) DEFAULT 0,
  `discord_webhook_id` int(11) DEFAULT 0,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `player_event_logs`
--

DROP TABLE IF EXISTS `player_event_logs`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `player_event_logs` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT,
  `account_id` bigint(20) DEFAULT NULL,
  `character_id` bigint(20) DEFAULT NULL,
  `zone_id` int(11) DEFAULT NULL,
  `instance_id` int(11) DEFAULT NULL,
  `x` float DEFAULT NULL,
  `y` float DEFAULT NULL,
  `z` float DEFAULT NULL,
  `heading` float DEFAULT NULL,
  `event_type_id` int(11) DEFAULT NULL,
  `event_type_name` varchar(255) DEFAULT NULL,
  `event_data` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_bin DEFAULT NULL,
  `created_at` datetime DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `event_created_at` (`event_type_id`,`created_at`),
  KEY `zone_id` (`zone_id`),
  KEY `character_id` (`character_id`,`zone_id`) USING BTREE,
  KEY `created_at` (`created_at`)
) ENGINE=InnoDB AUTO_INCREMENT=25 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `player_titlesets`
--

DROP TABLE IF EXISTS `player_titlesets`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `player_titlesets` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `char_id` int(11) unsigned NOT NULL,
  `title_set` int(11) unsigned NOT NULL,
  PRIMARY KEY (`id`),
  KEY `id` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `profanity_list`
--

DROP TABLE IF EXISTS `profanity_list`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `profanity_list` (
  `word` varchar(16) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `proximities_backup_9203`
--

DROP TABLE IF EXISTS `proximities_backup_9203`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `proximities_backup_9203` (
  `zoneid` int(10) unsigned NOT NULL DEFAULT 0,
  `exploreid` int(10) unsigned NOT NULL DEFAULT 0,
  `minx` float(14,6) NOT NULL DEFAULT 0.000000,
  `maxx` float(14,6) NOT NULL DEFAULT 0.000000,
  `miny` float(14,6) NOT NULL DEFAULT 0.000000,
  `maxy` float(14,6) NOT NULL DEFAULT 0.000000,
  `minz` float(14,6) NOT NULL DEFAULT 0.000000,
  `maxz` float(14,6) NOT NULL DEFAULT 0.000000,
  PRIMARY KEY (`zoneid`,`exploreid`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `qed_admin`
--

DROP TABLE IF EXISTS `qed_admin`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `qed_admin` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `login` varchar(30) NOT NULL DEFAULT '',
  `password` varchar(255) NOT NULL DEFAULT '',
  `access` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `qed_quests`
--

DROP TABLE IF EXISTS `qed_quests`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `qed_quests` (
  `quest_id` int(11) NOT NULL AUTO_INCREMENT,
  `quest_name` varchar(64) NOT NULL DEFAULT '',
  `startzone_id` int(11) NOT NULL DEFAULT 0,
  `questgiver_npcid` int(11) NOT NULL DEFAULT 0,
  `quest_status` int(10) NOT NULL DEFAULT 0,
  `quest_developer` varchar(30) NOT NULL DEFAULT 'Asram',
  `last_modified` timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  UNIQUE KEY `ID` (`quest_id`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `qed_zone`
--

DROP TABLE IF EXISTS `qed_zone`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `qed_zone` (
  `zoneidnumber` int(4) NOT NULL DEFAULT 0,
  `short_name` varchar(16) NOT NULL DEFAULT '',
  `long_name` text NOT NULL,
  `expansion` int(4) NOT NULL DEFAULT 0,
  PRIMARY KEY (`zoneidnumber`),
  UNIQUE KEY `zoneidnumber` (`zoneidnumber`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `qs_merchant_transaction_record`
--

DROP TABLE IF EXISTS `qs_merchant_transaction_record`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `qs_merchant_transaction_record` (
  `transaction_id` int(11) NOT NULL AUTO_INCREMENT,
  `time` timestamp NULL DEFAULT NULL ON UPDATE current_timestamp(),
  `zone_id` int(11) DEFAULT 0,
  `merchant_id` int(11) DEFAULT 0,
  `merchant_pp` int(11) DEFAULT 0,
  `merchant_gp` int(11) DEFAULT 0,
  `merchant_sp` int(11) DEFAULT 0,
  `merchant_cp` int(11) DEFAULT 0,
  `merchant_items` mediumint(7) DEFAULT 0,
  `char_id` int(11) DEFAULT 0,
  `char_pp` int(11) DEFAULT 0,
  `char_gp` int(11) DEFAULT 0,
  `char_sp` int(11) DEFAULT 0,
  `char_cp` int(11) DEFAULT 0,
  `char_items` mediumint(7) DEFAULT 0,
  PRIMARY KEY (`transaction_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `qs_merchant_transaction_record_entries`
--

DROP TABLE IF EXISTS `qs_merchant_transaction_record_entries`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `qs_merchant_transaction_record_entries` (
  `event_id` int(11) DEFAULT 0,
  `char_slot` mediumint(7) DEFAULT 0,
  `item_id` int(11) DEFAULT 0,
  `charges` mediumint(7) DEFAULT 0,
  `aug_1` int(11) DEFAULT 0,
  `aug_2` int(11) DEFAULT 0,
  `aug_3` int(11) DEFAULT 0,
  `aug_4` int(11) DEFAULT 0,
  `aug_5` int(11) DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `qs_player_aa_rate_hourly`
--

DROP TABLE IF EXISTS `qs_player_aa_rate_hourly`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `qs_player_aa_rate_hourly` (
  `char_id` int(11) NOT NULL DEFAULT 0,
  `hour_time` int(11) NOT NULL,
  `aa_count` varchar(11) DEFAULT NULL,
  PRIMARY KEY (`char_id`,`hour_time`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `qs_player_delete_record`
--

DROP TABLE IF EXISTS `qs_player_delete_record`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `qs_player_delete_record` (
  `delete_id` int(11) NOT NULL AUTO_INCREMENT,
  `time` timestamp NULL DEFAULT NULL ON UPDATE current_timestamp(),
  `char_id` int(11) DEFAULT 0,
  `stack_size` mediumint(7) DEFAULT 0,
  `char_items` mediumint(7) DEFAULT 0,
  PRIMARY KEY (`delete_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `qs_player_delete_record_entries`
--

DROP TABLE IF EXISTS `qs_player_delete_record_entries`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `qs_player_delete_record_entries` (
  `event_id` int(11) DEFAULT 0,
  `char_slot` mediumint(7) DEFAULT 0,
  `item_id` int(11) DEFAULT 0,
  `charges` mediumint(7) DEFAULT 0,
  `aug_1` int(11) DEFAULT 0,
  `aug_2` int(11) DEFAULT 0,
  `aug_3` int(11) DEFAULT 0,
  `aug_4` int(11) DEFAULT 0,
  `aug_5` int(11) DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `qs_player_events`
--

DROP TABLE IF EXISTS `qs_player_events`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `qs_player_events` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `char_id` int(11) DEFAULT 0,
  `event` int(11) unsigned DEFAULT 0,
  `event_desc` varchar(255) DEFAULT NULL,
  `time` int(11) unsigned DEFAULT 0,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `qs_player_handin_record`
--

DROP TABLE IF EXISTS `qs_player_handin_record`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `qs_player_handin_record` (
  `handin_id` int(11) NOT NULL AUTO_INCREMENT,
  `time` timestamp NULL DEFAULT NULL ON UPDATE current_timestamp(),
  `quest_id` int(11) DEFAULT 0,
  `char_id` int(11) DEFAULT 0,
  `char_pp` int(11) DEFAULT 0,
  `char_gp` int(11) DEFAULT 0,
  `char_sp` int(11) DEFAULT 0,
  `char_cp` int(11) DEFAULT 0,
  `char_items` mediumint(7) DEFAULT 0,
  `npc_id` int(11) DEFAULT 0,
  `npc_pp` int(11) DEFAULT 0,
  `npc_gp` int(11) DEFAULT 0,
  `npc_sp` int(11) DEFAULT 0,
  `npc_cp` int(11) DEFAULT 0,
  `npc_items` mediumint(7) DEFAULT 0,
  PRIMARY KEY (`handin_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `qs_player_handin_record_entries`
--

DROP TABLE IF EXISTS `qs_player_handin_record_entries`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `qs_player_handin_record_entries` (
  `event_id` int(11) DEFAULT 0,
  `action_type` char(6) DEFAULT 'action',
  `char_slot` mediumint(7) DEFAULT 0,
  `item_id` int(11) DEFAULT 0,
  `charges` mediumint(7) DEFAULT 0,
  `aug_1` int(11) DEFAULT 0,
  `aug_2` int(11) DEFAULT 0,
  `aug_3` int(11) DEFAULT 0,
  `aug_4` int(11) DEFAULT 0,
  `aug_5` int(11) DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `qs_player_move_record`
--

DROP TABLE IF EXISTS `qs_player_move_record`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `qs_player_move_record` (
  `move_id` int(11) NOT NULL AUTO_INCREMENT,
  `time` timestamp NULL DEFAULT NULL ON UPDATE current_timestamp(),
  `char_id` int(11) DEFAULT 0,
  `from_slot` mediumint(7) DEFAULT 0,
  `to_slot` mediumint(7) DEFAULT 0,
  `stack_size` mediumint(7) DEFAULT 0,
  `char_items` mediumint(7) DEFAULT 0,
  `postaction` tinyint(1) DEFAULT 0,
  PRIMARY KEY (`move_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `qs_player_move_record_entries`
--

DROP TABLE IF EXISTS `qs_player_move_record_entries`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `qs_player_move_record_entries` (
  `event_id` int(11) DEFAULT 0,
  `from_slot` mediumint(7) DEFAULT 0,
  `to_slot` mediumint(7) DEFAULT 0,
  `item_id` int(11) DEFAULT 0,
  `charges` mediumint(7) DEFAULT 0,
  `aug_1` int(11) DEFAULT 0,
  `aug_2` int(11) DEFAULT 0,
  `aug_3` int(11) DEFAULT 0,
  `aug_4` int(11) DEFAULT 0,
  `aug_5` int(11) DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `qs_player_npc_kill_record`
--

DROP TABLE IF EXISTS `qs_player_npc_kill_record`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `qs_player_npc_kill_record` (
  `fight_id` int(11) NOT NULL AUTO_INCREMENT,
  `npc_id` int(11) DEFAULT NULL,
  `type` int(11) DEFAULT NULL,
  `zone_id` int(11) DEFAULT NULL,
  `time` timestamp NULL DEFAULT NULL ON UPDATE current_timestamp(),
  PRIMARY KEY (`fight_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `qs_player_npc_kill_record_entries`
--

DROP TABLE IF EXISTS `qs_player_npc_kill_record_entries`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `qs_player_npc_kill_record_entries` (
  `event_id` int(11) DEFAULT 0,
  `char_id` int(11) DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `qs_player_speech`
--

DROP TABLE IF EXISTS `qs_player_speech`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `qs_player_speech` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `from` varchar(64) NOT NULL,
  `to` varchar(64) NOT NULL,
  `message` varchar(256) NOT NULL,
  `minstatus` smallint(5) NOT NULL,
  `guilddbid` int(11) NOT NULL,
  `type` tinyint(3) NOT NULL,
  `timerecorded` timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `qs_player_trade_record`
--

DROP TABLE IF EXISTS `qs_player_trade_record`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `qs_player_trade_record` (
  `trade_id` int(11) NOT NULL AUTO_INCREMENT,
  `time` timestamp NULL DEFAULT NULL ON UPDATE current_timestamp(),
  `char1_id` int(11) DEFAULT 0,
  `char1_pp` int(11) DEFAULT 0,
  `char1_gp` int(11) DEFAULT 0,
  `char1_sp` int(11) DEFAULT 0,
  `char1_cp` int(11) DEFAULT 0,
  `char1_items` mediumint(7) DEFAULT 0,
  `char2_id` int(11) DEFAULT 0,
  `char2_pp` int(11) DEFAULT 0,
  `char2_gp` int(11) DEFAULT 0,
  `char2_sp` int(11) DEFAULT 0,
  `char2_cp` int(11) DEFAULT 0,
  `char2_items` mediumint(7) DEFAULT 0,
  PRIMARY KEY (`trade_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `qs_player_trade_record_entries`
--

DROP TABLE IF EXISTS `qs_player_trade_record_entries`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `qs_player_trade_record_entries` (
  `event_id` int(11) DEFAULT 0,
  `from_id` int(11) DEFAULT 0,
  `from_slot` mediumint(7) DEFAULT 0,
  `to_id` int(11) DEFAULT 0,
  `to_slot` mediumint(7) DEFAULT 0,
  `item_id` int(11) DEFAULT 0,
  `charges` mediumint(7) DEFAULT 0,
  `aug_1` int(11) DEFAULT 0,
  `aug_2` int(11) DEFAULT 0,
  `aug_3` int(11) DEFAULT 0,
  `aug_4` int(11) DEFAULT 0,
  `aug_5` int(11) DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `quest_globals`
--

DROP TABLE IF EXISTS `quest_globals`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `quest_globals` (
  `charid` int(11) NOT NULL DEFAULT 0,
  `npcid` int(11) NOT NULL DEFAULT 0,
  `zoneid` int(11) NOT NULL DEFAULT 0,
  `name` varchar(65) NOT NULL DEFAULT '',
  `value` varchar(128) CHARACTER SET latin1 COLLATE latin1_swedish_ci NOT NULL DEFAULT '?',
  `expdate` int(11) DEFAULT NULL,
  PRIMARY KEY (`charid`,`npcid`,`zoneid`,`name`),
  UNIQUE KEY `qname` (`name`,`charid`,`npcid`,`zoneid`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `quest_items`
--

DROP TABLE IF EXISTS `quest_items`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `quest_items` (
  `item_id` int(11) NOT NULL DEFAULT 0,
  `npc` varchar(64) NOT NULL DEFAULT '',
  `zone` varchar(64) NOT NULL DEFAULT '',
  `rewarded` tinyint(4) NOT NULL DEFAULT 0,
  `handed` tinyint(4) NOT NULL DEFAULT 0,
  PRIMARY KEY (`item_id`,`npc`,`zone`),
  KEY `item_id` (`item_id`)
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `races`
--

DROP TABLE IF EXISTS `races`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `races` (
  `name` varchar(64) NOT NULL DEFAULT '',
  `id` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`name`),
  KEY `id` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `raid_details`
--

DROP TABLE IF EXISTS `raid_details`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `raid_details` (
  `raidid` int(4) NOT NULL DEFAULT 0,
  `loottype` int(4) NOT NULL DEFAULT 0,
  `locked` tinyint(1) NOT NULL DEFAULT 0,
  `motd` varchar(1024) DEFAULT NULL,
  `marked_npc_1_entity_id` int(10) unsigned NOT NULL DEFAULT 0,
  `marked_npc_1_zone_id` int(10) unsigned NOT NULL DEFAULT 0,
  `marked_npc_1_instance_id` int(10) unsigned NOT NULL DEFAULT 0,
  `marked_npc_2_entity_id` int(10) unsigned NOT NULL DEFAULT 0,
  `marked_npc_2_zone_id` int(10) unsigned NOT NULL DEFAULT 0,
  `marked_npc_2_instance_id` int(10) unsigned NOT NULL DEFAULT 0,
  `marked_npc_3_entity_id` int(10) unsigned NOT NULL DEFAULT 0,
  `marked_npc_3_zone_id` int(10) unsigned NOT NULL DEFAULT 0,
  `marked_npc_3_instance_id` int(10) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`raidid`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `raid_groups`
--

DROP TABLE IF EXISTS `raid_groups`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `raid_groups` (
  `raidid` int(4) NOT NULL DEFAULT 0,
  `groupid` int(4) NOT NULL DEFAULT 0,
  `groupindex` tinyint(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`raidid`,`groupid`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `raid_leader`
--

DROP TABLE IF EXISTS `raid_leader`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `raid_leader` (
  `raidid` int(4) NOT NULL DEFAULT 0,
  `name` varchar(64) NOT NULL DEFAULT '',
  PRIMARY KEY (`raidid`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `raid_leaders`
--

DROP TABLE IF EXISTS `raid_leaders`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `raid_leaders` (
  `gid` int(4) unsigned NOT NULL,
  `rid` int(4) unsigned NOT NULL,
  `marknpc` varchar(64) NOT NULL,
  `maintank` varchar(64) NOT NULL,
  `assist` varchar(64) NOT NULL,
  `puller` varchar(64) NOT NULL,
  `leadershipaa` tinyblob NOT NULL,
  `mentoree` varchar(64) NOT NULL,
  `mentor_percent` int(4) NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `raid_members`
--

DROP TABLE IF EXISTS `raid_members`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `raid_members` (
  `id` bigint(20) unsigned NOT NULL AUTO_INCREMENT,
  `raidid` int(4) NOT NULL DEFAULT 0,
  `charid` int(4) NOT NULL DEFAULT 0,
  `bot_id` int(4) NOT NULL DEFAULT 0,
  `groupid` int(4) unsigned NOT NULL DEFAULT 0,
  `_class` int(4) NOT NULL DEFAULT 0,
  `level` int(4) NOT NULL DEFAULT 0,
  `name` varchar(64) NOT NULL DEFAULT '',
  `isgroupleader` tinyint(1) NOT NULL DEFAULT 0,
  `israidleader` tinyint(1) NOT NULL DEFAULT 0,
  `islooter` tinyint(1) NOT NULL DEFAULT 0,
  `is_marker` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `is_assister` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `note` varchar(64) NOT NULL DEFAULT '',
  PRIMARY KEY (`id`),
  UNIQUE KEY `UNIQUE` (`name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `raid_ungrouped_members`
--

DROP TABLE IF EXISTS `raid_ungrouped_members`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `raid_ungrouped_members` (
  `raidid` int(4) NOT NULL DEFAULT 0,
  `charid` int(4) NOT NULL DEFAULT 0,
  `name` varchar(64) NOT NULL DEFAULT '',
  `ismaintank` tinyint(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`raidid`,`charid`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `reports`
--

DROP TABLE IF EXISTS `reports`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `reports` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `name` varchar(64) DEFAULT NULL,
  `reported` varchar(64) DEFAULT NULL,
  `reported_text` text DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `id` (`id`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `respawn_times`
--

DROP TABLE IF EXISTS `respawn_times`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `respawn_times` (
  `id` int(11) NOT NULL DEFAULT 0,
  `start` int(11) NOT NULL DEFAULT 0,
  `duration` int(11) NOT NULL DEFAULT 0,
  `instance_id` smallint(6) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`,`instance_id`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `rule_sets`
--

DROP TABLE IF EXISTS `rule_sets`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `rule_sets` (
  `ruleset_id` tinyint(3) unsigned NOT NULL AUTO_INCREMENT,
  `name` varchar(255) NOT NULL DEFAULT '',
  PRIMARY KEY (`ruleset_id`)
) ENGINE=InnoDB AUTO_INCREMENT=2 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `rule_values`
--

DROP TABLE IF EXISTS `rule_values`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `rule_values` (
  `ruleset_id` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `rule_name` varchar(64) NOT NULL DEFAULT '',
  `rule_value` text DEFAULT NULL,
  `notes` text DEFAULT NULL,
  PRIMARY KEY (`ruleset_id`,`rule_name`),
  KEY `ruleset_id` (`ruleset_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `saylink`
--

DROP TABLE IF EXISTS `saylink`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `saylink` (
  `id` int(10) NOT NULL AUTO_INCREMENT,
  `phrase` varchar(64) NOT NULL DEFAULT '',
  PRIMARY KEY (`id`),
  KEY `phrase_index` (`phrase`) USING BTREE
) ENGINE=InnoDB AUTO_INCREMENT=23043 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_bin;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `server_scheduled_events`
--

DROP TABLE IF EXISTS `server_scheduled_events`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `server_scheduled_events` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `description` varchar(255) DEFAULT NULL,
  `event_type` varchar(100) DEFAULT NULL,
  `event_data` text DEFAULT NULL,
  `minute_start` int(11) DEFAULT 0,
  `hour_start` int(11) DEFAULT 0,
  `day_start` int(11) DEFAULT 0,
  `month_start` int(11) DEFAULT 0,
  `year_start` int(11) DEFAULT 0,
  `minute_end` int(11) DEFAULT 0,
  `hour_end` int(11) DEFAULT 0,
  `day_end` int(11) DEFAULT 0,
  `month_end` int(11) DEFAULT 0,
  `year_end` int(11) DEFAULT 0,
  `cron_expression` varchar(100) DEFAULT NULL,
  `created_at` datetime DEFAULT NULL,
  `deleted_at` datetime DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `shared_task_activity_state`
--

DROP TABLE IF EXISTS `shared_task_activity_state`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `shared_task_activity_state` (
  `shared_task_id` bigint(20) NOT NULL,
  `activity_id` int(11) NOT NULL,
  `done_count` int(11) DEFAULT NULL,
  `updated_time` datetime DEFAULT NULL,
  `completed_time` datetime DEFAULT NULL,
  PRIMARY KEY (`shared_task_id`,`activity_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `shared_task_dynamic_zones`
--

DROP TABLE IF EXISTS `shared_task_dynamic_zones`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `shared_task_dynamic_zones` (
  `shared_task_id` bigint(20) NOT NULL,
  `dynamic_zone_id` int(10) unsigned NOT NULL,
  PRIMARY KEY (`shared_task_id`,`dynamic_zone_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `shared_task_members`
--

DROP TABLE IF EXISTS `shared_task_members`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `shared_task_members` (
  `shared_task_id` bigint(20) NOT NULL,
  `character_id` bigint(20) NOT NULL,
  `is_leader` tinyint(4) DEFAULT NULL,
  PRIMARY KEY (`shared_task_id`,`character_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `shared_tasks`
--

DROP TABLE IF EXISTS `shared_tasks`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `shared_tasks` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT,
  `task_id` int(11) DEFAULT NULL,
  `accepted_time` datetime DEFAULT NULL,
  `expire_time` datetime DEFAULT NULL,
  `completion_time` datetime DEFAULT NULL,
  `is_locked` tinyint(1) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `sharedbank`
--

DROP TABLE IF EXISTS `sharedbank`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `sharedbank` (
  `acctid` int(11) unsigned DEFAULT 0,
  `slotid` mediumint(7) unsigned DEFAULT 0,
  `itemid` int(11) unsigned DEFAULT 0,
  `charges` smallint(3) unsigned DEFAULT 0,
  `augslot1` mediumint(7) unsigned NOT NULL DEFAULT 0,
  `augslot2` mediumint(7) unsigned NOT NULL DEFAULT 0,
  `augslot3` mediumint(7) unsigned NOT NULL DEFAULT 0,
  `augslot4` mediumint(7) unsigned NOT NULL DEFAULT 0,
  `augslot5` mediumint(7) unsigned NOT NULL DEFAULT 0,
  `augslot6` mediumint(7) NOT NULL DEFAULT 0,
  `custom_data` text DEFAULT NULL,
  UNIQUE KEY `account` (`acctid`,`slotid`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `skill_caps`
--

DROP TABLE IF EXISTS `skill_caps`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `skill_caps` (
  `id` int(3) unsigned NOT NULL AUTO_INCREMENT,
  `skill_id` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `class_id` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `level` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `cap` mediumint(8) unsigned NOT NULL DEFAULT 0,
  `class_` tinyint(3) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`) USING BTREE,
  KEY `level_skill_cap` (`skill_id`,`class_id`,`level`,`cap`)
) ENGINE=InnoDB AUTO_INCREMENT=40652 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `spawn2`
--

DROP TABLE IF EXISTS `spawn2`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `spawn2` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `spawngroupID` int(11) NOT NULL DEFAULT 0,
  `zone` varchar(32) DEFAULT NULL,
  `version` smallint(5) NOT NULL DEFAULT 0,
  `x` float(14,6) NOT NULL DEFAULT 0.000000,
  `y` float(14,6) NOT NULL DEFAULT 0.000000,
  `z` float(14,6) NOT NULL DEFAULT 0.000000,
  `heading` float(14,6) NOT NULL DEFAULT 0.000000,
  `respawntime` int(11) NOT NULL DEFAULT 0,
  `variance` int(11) NOT NULL DEFAULT 0,
  `pathgrid` int(10) NOT NULL DEFAULT 0,
  `path_when_zone_idle` tinyint(1) NOT NULL DEFAULT 0,
  `_condition` mediumint(8) unsigned NOT NULL DEFAULT 0,
  `cond_value` mediumint(9) NOT NULL DEFAULT 1,
  `animation` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `min_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `max_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `content_flags` varchar(100) DEFAULT NULL,
  `content_flags_disabled` varchar(100) DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `ZoneGroup` (`zone`)
) ENGINE=MyISAM AUTO_INCREMENT=227632 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `spawn2_backup_2023_10_29`
--

DROP TABLE IF EXISTS `spawn2_backup_2023_10_29`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `spawn2_backup_2023_10_29` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `spawngroupID` int(11) NOT NULL DEFAULT 0,
  `zone` varchar(32) DEFAULT NULL,
  `version` smallint(5) NOT NULL DEFAULT 0,
  `x` float(14,6) NOT NULL DEFAULT 0.000000,
  `y` float(14,6) NOT NULL DEFAULT 0.000000,
  `z` float(14,6) NOT NULL DEFAULT 0.000000,
  `heading` float(14,6) NOT NULL DEFAULT 0.000000,
  `respawntime` int(11) NOT NULL DEFAULT 0,
  `variance` int(11) NOT NULL DEFAULT 0,
  `pathgrid` int(10) NOT NULL DEFAULT 0,
  `path_when_zone_idle` tinyint(1) NOT NULL DEFAULT 0,
  `_condition` mediumint(8) unsigned NOT NULL DEFAULT 0,
  `cond_value` mediumint(9) NOT NULL DEFAULT 1,
  `enabled` tinyint(3) unsigned NOT NULL DEFAULT 1,
  `animation` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `min_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `max_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `content_flags` varchar(100) DEFAULT NULL,
  `content_flags_disabled` varchar(100) DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `ZoneGroup` (`zone`)
) ENGINE=MyISAM AUTO_INCREMENT=227555 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `spawn2_disabled`
--

DROP TABLE IF EXISTS `spawn2_disabled`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `spawn2_disabled` (
  `id` bigint(11) NOT NULL AUTO_INCREMENT,
  `spawn2_id` int(11) DEFAULT NULL,
  `instance_id` int(11) DEFAULT 0,
  `disabled` smallint(11) DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `spawn2_id` (`spawn2_id`,`instance_id`) USING BTREE
) ENGINE=InnoDB AUTO_INCREMENT=39 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `spawn_condition_values`
--

DROP TABLE IF EXISTS `spawn_condition_values`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `spawn_condition_values` (
  `id` int(10) unsigned NOT NULL,
  `value` tinyint(3) unsigned DEFAULT NULL,
  `zone` varchar(64) NOT NULL,
  `instance_id` int(10) unsigned NOT NULL,
  UNIQUE KEY `instance` (`id`,`instance_id`,`zone`),
  KEY `zoneinstance` (`zone`,`instance_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `spawn_conditions`
--

DROP TABLE IF EXISTS `spawn_conditions`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `spawn_conditions` (
  `zone` varchar(16) NOT NULL DEFAULT '',
  `id` mediumint(8) unsigned NOT NULL DEFAULT 1,
  `value` mediumint(9) NOT NULL DEFAULT 0,
  `onchange` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `name` varchar(255) NOT NULL DEFAULT '',
  PRIMARY KEY (`zone`,`id`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `spawn_events`
--

DROP TABLE IF EXISTS `spawn_events`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `spawn_events` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `zone` varchar(16) NOT NULL DEFAULT '',
  `cond_id` mediumint(8) unsigned NOT NULL DEFAULT 0,
  `name` varchar(255) NOT NULL DEFAULT '',
  `period` int(10) unsigned NOT NULL DEFAULT 0,
  `next_minute` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `next_hour` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `next_day` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `next_month` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `next_year` int(10) unsigned NOT NULL DEFAULT 0,
  `enabled` tinyint(4) NOT NULL DEFAULT 1,
  `action` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `argument` mediumint(9) NOT NULL DEFAULT 0,
  `strict` tinyint(4) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`)
) ENGINE=MyISAM AUTO_INCREMENT=487 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `spawnentry`
--

DROP TABLE IF EXISTS `spawnentry`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `spawnentry` (
  `spawngroupID` int(11) NOT NULL DEFAULT 0,
  `npcID` int(11) NOT NULL DEFAULT 0,
  `chance` smallint(4) NOT NULL DEFAULT 0,
  `condition_value_filter` mediumint(9) NOT NULL DEFAULT 1,
  `min_time` smallint(4) NOT NULL DEFAULT 0,
  `max_time` smallint(4) NOT NULL DEFAULT 0,
  `min_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `max_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `content_flags` varchar(100) DEFAULT NULL,
  `content_flags_disabled` varchar(100) DEFAULT NULL,
  PRIMARY KEY (`spawngroupID`,`npcID`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `spawngroup`
--

DROP TABLE IF EXISTS `spawngroup`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `spawngroup` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(30) NOT NULL DEFAULT '',
  `spawn_limit` tinyint(4) NOT NULL DEFAULT 0,
  `dist` float NOT NULL DEFAULT 0,
  `max_x` float NOT NULL DEFAULT 0,
  `min_x` float NOT NULL DEFAULT 0,
  `max_y` float NOT NULL DEFAULT 0,
  `min_y` float NOT NULL DEFAULT 0,
  `delay` int(11) NOT NULL DEFAULT 45000,
  `mindelay` int(11) NOT NULL DEFAULT 15000,
  `despawn` tinyint(3) NOT NULL DEFAULT 0,
  `despawn_timer` int(11) NOT NULL DEFAULT 100,
  `wp_spawns` tinyint(1) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `name` (`name`)
) ENGINE=MyISAM AUTO_INCREMENT=243867 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `spell_buckets`
--

DROP TABLE IF EXISTS `spell_buckets`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `spell_buckets` (
  `spellid` bigint(11) unsigned NOT NULL,
  `key` varchar(100) DEFAULT NULL,
  `value` text DEFAULT NULL,
  PRIMARY KEY (`spellid`),
  KEY `key_index` (`key`) USING BTREE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `spells`
--

DROP TABLE IF EXISTS `spells`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `spells` (
  `spellid` int(11) NOT NULL DEFAULT 0,
  `name` varchar(64) NOT NULL DEFAULT '',
  `castonyou` varchar(120) NOT NULL DEFAULT '',
  `castonother` varchar(120) NOT NULL DEFAULT '',
  `fading` varchar(120) NOT NULL DEFAULT '',
  `range` int(11) NOT NULL DEFAULT 0,
  `aoerange` int(11) NOT NULL DEFAULT 0,
  `mana` int(11) NOT NULL DEFAULT 0,
  `casttime` int(11) NOT NULL DEFAULT 0,
  `recoverytime` int(11) NOT NULL DEFAULT 0,
  `recasttime` int(11) NOT NULL DEFAULT 0,
  `buffformula` int(11) NOT NULL DEFAULT 0,
  `buffduration` int(11) NOT NULL DEFAULT 0,
  `aoeduration` int(11) NOT NULL DEFAULT 0,
  `targettype` int(11) NOT NULL DEFAULT 0,
  `petname` varchar(32) NOT NULL DEFAULT '',
  `resist` int(11) NOT NULL DEFAULT 0,
  `resist_adjust` int(11) NOT NULL DEFAULT 0,
  `skill` int(11) NOT NULL DEFAULT 0,
  `buff1` int(11) NOT NULL DEFAULT 0,
  `buff1val` int(11) NOT NULL DEFAULT 0,
  `buff2` int(11) NOT NULL DEFAULT 0,
  `buff2val` int(11) NOT NULL DEFAULT 0,
  `buff3` int(11) NOT NULL DEFAULT 0,
  `buff3val` int(11) NOT NULL DEFAULT 0,
  `buff4` int(11) NOT NULL DEFAULT 0,
  `buff4val` int(11) NOT NULL DEFAULT 0,
  `buff5` int(11) NOT NULL DEFAULT 0,
  `buff5val` int(11) NOT NULL DEFAULT 0,
  `buff6` int(11) NOT NULL DEFAULT 0,
  `buff6val` int(11) NOT NULL DEFAULT 0,
  `buff7` int(11) NOT NULL DEFAULT 0,
  `buff7val` int(11) NOT NULL DEFAULT 0,
  `buff8` int(11) NOT NULL DEFAULT 0,
  `buff8val` int(11) NOT NULL DEFAULT 0,
  `buff9` int(11) NOT NULL DEFAULT 0,
  `buff9val` int(11) NOT NULL DEFAULT 0,
  `buff10` int(11) NOT NULL DEFAULT 0,
  `buff10val` int(11) NOT NULL DEFAULT 0,
  `buff11` int(11) NOT NULL DEFAULT 0,
  `buff11val` int(11) NOT NULL DEFAULT 0,
  `buff12` int(11) NOT NULL DEFAULT 0,
  `buff12val` int(11) NOT NULL DEFAULT 0,
  `buff1form` int(11) NOT NULL DEFAULT 0,
  `buff2form` int(11) NOT NULL DEFAULT 0,
  `buff3form` int(11) NOT NULL DEFAULT 0,
  `buff4form` int(11) NOT NULL DEFAULT 0,
  `buff5form` int(11) NOT NULL DEFAULT 0,
  `buff6form` int(11) NOT NULL DEFAULT 0,
  `buff7form` int(11) NOT NULL DEFAULT 0,
  `buff8form` int(11) NOT NULL DEFAULT 0,
  `buff9form` int(11) NOT NULL DEFAULT 0,
  `buff10form` int(11) NOT NULL DEFAULT 0,
  `buff11form` int(11) NOT NULL DEFAULT 0,
  `buff12form` int(11) NOT NULL DEFAULT 0,
  `buff1max` int(11) NOT NULL DEFAULT 0,
  `buff2max` int(11) NOT NULL DEFAULT 0,
  `buff3max` int(11) NOT NULL DEFAULT 0,
  `buff4max` int(11) NOT NULL DEFAULT 0,
  `buff5max` int(11) NOT NULL DEFAULT 0,
  `buff6max` int(11) NOT NULL DEFAULT 0,
  `buff7max` int(11) NOT NULL DEFAULT 0,
  `buff8max` int(11) NOT NULL DEFAULT 0,
  `buff9max` int(11) NOT NULL DEFAULT 0,
  `buff10max` int(11) NOT NULL DEFAULT 0,
  `buff11max` int(11) NOT NULL DEFAULT 0,
  `buff12max` int(11) NOT NULL DEFAULT 0,
  `level1` int(4) NOT NULL DEFAULT 0,
  `level2` int(4) NOT NULL DEFAULT 0,
  `level3` int(4) NOT NULL DEFAULT 0,
  `level4` int(4) NOT NULL DEFAULT 0,
  `level5` int(4) NOT NULL DEFAULT 0,
  `level6` int(4) NOT NULL DEFAULT 0,
  `level7` int(4) NOT NULL DEFAULT 0,
  `level8` int(4) NOT NULL DEFAULT 0,
  `level9` int(4) NOT NULL DEFAULT 0,
  `level10` int(4) NOT NULL DEFAULT 0,
  `level11` int(4) NOT NULL DEFAULT 0,
  `level12` int(4) NOT NULL DEFAULT 0,
  `level13` int(4) NOT NULL DEFAULT 0,
  `level14` int(4) NOT NULL DEFAULT 0,
  `level15` int(4) NOT NULL DEFAULT 0,
  `level16` int(4) NOT NULL DEFAULT 0,
  `regent1_id` int(11) NOT NULL DEFAULT 0,
  `regent1_count` int(11) NOT NULL DEFAULT 0,
  `regent2_id` int(11) NOT NULL DEFAULT 0,
  `regent2_count` int(11) NOT NULL DEFAULT 0,
  `regent3_id` int(11) NOT NULL DEFAULT 0,
  `regent3_count` int(11) NOT NULL DEFAULT 0,
  `regent4_id` int(11) NOT NULL DEFAULT 0,
  `regent4_count` int(11) NOT NULL DEFAULT 0,
  `timeofday` int(11) NOT NULL DEFAULT 0,
  UNIQUE KEY `spellid` (`spellid`)
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `spells_new`
--

DROP TABLE IF EXISTS `spells_new`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `spells_new` (
  `id` int(11) NOT NULL DEFAULT 0,
  `name` varchar(64) DEFAULT NULL,
  `player_1` varchar(64) DEFAULT 'BLUE_TRAIL',
  `teleport_zone` varchar(64) DEFAULT NULL,
  `you_cast` varchar(120) DEFAULT NULL,
  `other_casts` varchar(120) DEFAULT NULL,
  `cast_on_you` varchar(120) DEFAULT NULL,
  `cast_on_other` varchar(120) DEFAULT NULL,
  `spell_fades` varchar(120) DEFAULT NULL,
  `range` int(11) NOT NULL DEFAULT 100,
  `aoerange` int(11) NOT NULL DEFAULT 0,
  `pushback` int(11) NOT NULL DEFAULT 0,
  `pushup` int(11) NOT NULL DEFAULT 0,
  `cast_time` int(11) NOT NULL DEFAULT 0,
  `recovery_time` int(11) NOT NULL DEFAULT 0,
  `recast_time` int(11) NOT NULL DEFAULT 0,
  `buffdurationformula` int(11) NOT NULL DEFAULT 7,
  `buffduration` int(11) NOT NULL DEFAULT 65,
  `AEDuration` int(11) NOT NULL DEFAULT 0,
  `mana` int(11) NOT NULL DEFAULT 0,
  `effect_base_value1` int(11) NOT NULL DEFAULT 100,
  `effect_base_value2` int(11) NOT NULL DEFAULT 0,
  `effect_base_value3` int(11) NOT NULL DEFAULT 0,
  `effect_base_value4` int(11) NOT NULL DEFAULT 0,
  `effect_base_value5` int(11) NOT NULL DEFAULT 0,
  `effect_base_value6` int(11) NOT NULL DEFAULT 0,
  `effect_base_value7` int(11) NOT NULL DEFAULT 0,
  `effect_base_value8` int(11) NOT NULL DEFAULT 0,
  `effect_base_value9` int(11) NOT NULL DEFAULT 0,
  `effect_base_value10` int(11) NOT NULL DEFAULT 0,
  `effect_base_value11` int(11) NOT NULL DEFAULT 0,
  `effect_base_value12` int(11) NOT NULL DEFAULT 0,
  `effect_limit_value1` int(11) NOT NULL DEFAULT 0,
  `effect_limit_value2` int(11) NOT NULL DEFAULT 0,
  `effect_limit_value3` int(11) NOT NULL DEFAULT 0,
  `effect_limit_value4` int(11) NOT NULL DEFAULT 0,
  `effect_limit_value5` int(11) NOT NULL DEFAULT 0,
  `effect_limit_value6` int(11) NOT NULL DEFAULT 0,
  `effect_limit_value7` int(11) NOT NULL DEFAULT 0,
  `effect_limit_value8` int(11) NOT NULL DEFAULT 0,
  `effect_limit_value9` int(11) NOT NULL DEFAULT 0,
  `effect_limit_value10` int(11) NOT NULL DEFAULT 0,
  `effect_limit_value11` int(11) NOT NULL DEFAULT 0,
  `effect_limit_value12` int(11) NOT NULL DEFAULT 0,
  `max1` int(11) NOT NULL DEFAULT 0,
  `max2` int(11) NOT NULL DEFAULT 0,
  `max3` int(11) NOT NULL DEFAULT 0,
  `max4` int(11) NOT NULL DEFAULT 0,
  `max5` int(11) NOT NULL DEFAULT 0,
  `max6` int(11) NOT NULL DEFAULT 0,
  `max7` int(11) NOT NULL DEFAULT 0,
  `max8` int(11) NOT NULL DEFAULT 0,
  `max9` int(11) NOT NULL DEFAULT 0,
  `max10` int(11) NOT NULL DEFAULT 0,
  `max11` int(11) NOT NULL DEFAULT 0,
  `max12` int(11) NOT NULL DEFAULT 0,
  `icon` int(11) NOT NULL DEFAULT 0,
  `memicon` int(11) NOT NULL DEFAULT 0,
  `components1` int(11) NOT NULL DEFAULT -1,
  `components2` int(11) NOT NULL DEFAULT -1,
  `components3` int(11) NOT NULL DEFAULT -1,
  `components4` int(11) NOT NULL DEFAULT -1,
  `component_counts1` int(11) NOT NULL DEFAULT 1,
  `component_counts2` int(11) NOT NULL DEFAULT 1,
  `component_counts3` int(11) NOT NULL DEFAULT 1,
  `component_counts4` int(11) NOT NULL DEFAULT 1,
  `NoexpendReagent1` int(11) NOT NULL DEFAULT -1,
  `NoexpendReagent2` int(11) NOT NULL DEFAULT -1,
  `NoexpendReagent3` int(11) NOT NULL DEFAULT -1,
  `NoexpendReagent4` int(11) NOT NULL DEFAULT -1,
  `formula1` int(11) NOT NULL DEFAULT 100,
  `formula2` int(11) NOT NULL DEFAULT 100,
  `formula3` int(11) NOT NULL DEFAULT 100,
  `formula4` int(11) NOT NULL DEFAULT 100,
  `formula5` int(11) NOT NULL DEFAULT 100,
  `formula6` int(11) NOT NULL DEFAULT 100,
  `formula7` int(11) NOT NULL DEFAULT 100,
  `formula8` int(11) NOT NULL DEFAULT 100,
  `formula9` int(11) NOT NULL DEFAULT 100,
  `formula10` int(11) NOT NULL DEFAULT 100,
  `formula11` int(11) NOT NULL DEFAULT 100,
  `formula12` int(11) NOT NULL DEFAULT 100,
  `LightType` int(11) NOT NULL DEFAULT 0,
  `goodEffect` int(11) NOT NULL DEFAULT 0,
  `Activated` int(11) NOT NULL DEFAULT 0,
  `resisttype` int(11) NOT NULL DEFAULT 0,
  `effectid1` int(11) NOT NULL DEFAULT 254,
  `effectid2` int(11) NOT NULL DEFAULT 254,
  `effectid3` int(11) NOT NULL DEFAULT 254,
  `effectid4` int(11) NOT NULL DEFAULT 254,
  `effectid5` int(11) NOT NULL DEFAULT 254,
  `effectid6` int(11) NOT NULL DEFAULT 254,
  `effectid7` int(11) NOT NULL DEFAULT 254,
  `effectid8` int(11) NOT NULL DEFAULT 254,
  `effectid9` int(11) NOT NULL DEFAULT 254,
  `effectid10` int(11) NOT NULL DEFAULT 254,
  `effectid11` int(11) NOT NULL DEFAULT 254,
  `effectid12` int(11) NOT NULL DEFAULT 254,
  `targettype` int(11) NOT NULL DEFAULT 2,
  `basediff` int(11) NOT NULL DEFAULT 0,
  `skill` int(11) NOT NULL DEFAULT 98,
  `zonetype` int(11) NOT NULL DEFAULT -1,
  `EnvironmentType` int(11) NOT NULL DEFAULT 0,
  `TimeOfDay` int(11) NOT NULL DEFAULT 0,
  `classes1` int(11) NOT NULL DEFAULT 255,
  `classes2` int(11) NOT NULL DEFAULT 255,
  `classes3` int(11) NOT NULL DEFAULT 255,
  `classes4` int(11) NOT NULL DEFAULT 255,
  `classes5` int(11) NOT NULL DEFAULT 255,
  `classes6` int(11) NOT NULL DEFAULT 255,
  `classes7` int(11) NOT NULL DEFAULT 255,
  `classes8` int(11) NOT NULL DEFAULT 255,
  `classes9` int(11) NOT NULL DEFAULT 255,
  `classes10` int(11) NOT NULL DEFAULT 255,
  `classes11` int(11) NOT NULL DEFAULT 255,
  `classes12` int(11) NOT NULL DEFAULT 255,
  `classes13` int(11) NOT NULL DEFAULT 255,
  `classes14` int(11) NOT NULL DEFAULT 255,
  `classes15` int(11) NOT NULL DEFAULT 255,
  `classes16` int(11) NOT NULL DEFAULT 255,
  `CastingAnim` int(11) NOT NULL DEFAULT 44,
  `TargetAnim` int(11) NOT NULL DEFAULT 13,
  `TravelType` int(11) NOT NULL DEFAULT 0,
  `SpellAffectIndex` int(11) NOT NULL DEFAULT -1,
  `disallow_sit` int(11) NOT NULL DEFAULT 0,
  `deities0` int(11) NOT NULL DEFAULT 0,
  `deities1` int(11) NOT NULL DEFAULT 0,
  `deities2` int(11) NOT NULL DEFAULT 0,
  `deities3` int(11) NOT NULL DEFAULT 0,
  `deities4` int(11) NOT NULL DEFAULT 0,
  `deities5` int(11) NOT NULL DEFAULT 0,
  `deities6` int(11) NOT NULL DEFAULT 0,
  `deities7` int(11) NOT NULL DEFAULT 0,
  `deities8` int(11) NOT NULL DEFAULT 0,
  `deities9` int(11) NOT NULL DEFAULT 0,
  `deities10` int(11) NOT NULL DEFAULT 0,
  `deities11` int(11) NOT NULL DEFAULT 0,
  `deities12` int(12) NOT NULL DEFAULT 0,
  `deities13` int(11) NOT NULL DEFAULT 0,
  `deities14` int(11) NOT NULL DEFAULT 0,
  `deities15` int(11) NOT NULL DEFAULT 0,
  `deities16` int(11) NOT NULL DEFAULT 0,
  `field142` int(11) NOT NULL DEFAULT 100,
  `field143` int(11) NOT NULL DEFAULT 0,
  `new_icon` int(11) NOT NULL DEFAULT 161,
  `spellanim` int(11) NOT NULL DEFAULT 0,
  `uninterruptable` int(11) NOT NULL DEFAULT 0,
  `ResistDiff` int(11) NOT NULL DEFAULT -150,
  `dot_stacking_exempt` int(11) NOT NULL DEFAULT 0,
  `deleteable` int(11) NOT NULL DEFAULT 0,
  `RecourseLink` int(11) NOT NULL DEFAULT 0,
  `no_partial_resist` int(11) NOT NULL DEFAULT 0,
  `field152` int(11) NOT NULL DEFAULT 0,
  `field153` int(11) NOT NULL DEFAULT 0,
  `short_buff_box` int(11) NOT NULL DEFAULT -1,
  `descnum` int(11) NOT NULL DEFAULT 0,
  `typedescnum` int(11) DEFAULT NULL,
  `effectdescnum` int(11) DEFAULT NULL,
  `effectdescnum2` int(11) NOT NULL DEFAULT 0,
  `npc_no_los` int(11) NOT NULL DEFAULT 0,
  `field160` int(11) NOT NULL DEFAULT 0,
  `reflectable` int(11) NOT NULL DEFAULT 0,
  `bonushate` int(11) NOT NULL DEFAULT 0,
  `field163` int(11) NOT NULL DEFAULT 100,
  `field164` int(11) NOT NULL DEFAULT -150,
  `ldon_trap` int(11) NOT NULL DEFAULT 0,
  `EndurCost` int(11) NOT NULL DEFAULT 0,
  `EndurTimerIndex` int(11) NOT NULL DEFAULT 0,
  `IsDiscipline` int(11) NOT NULL DEFAULT 0,
  `field169` int(11) NOT NULL DEFAULT 0,
  `field170` int(11) NOT NULL DEFAULT 0,
  `field171` int(11) NOT NULL DEFAULT 0,
  `field172` int(11) NOT NULL DEFAULT 0,
  `HateAdded` int(11) NOT NULL DEFAULT 0,
  `EndurUpkeep` int(11) NOT NULL DEFAULT 0,
  `field175` int(11) DEFAULT NULL,
  `numhits` int(11) NOT NULL DEFAULT 0,
  `pvpresistbase` int(11) NOT NULL DEFAULT -150,
  `pvpresistcalc` int(11) NOT NULL DEFAULT 100,
  `pvpresistcap` int(11) NOT NULL DEFAULT -150,
  `spell_category` int(11) NOT NULL DEFAULT -99,
  `pvp_duration` int(11) NOT NULL DEFAULT 0,
  `pvp_duration_cap` int(11) NOT NULL DEFAULT 0,
  `pcnpc_only_flag` int(11) DEFAULT 0,
  `cast_not_standing` int(11) DEFAULT 0,
  `can_mgb` int(11) NOT NULL DEFAULT 0,
  `nodispell` int(11) NOT NULL DEFAULT -1,
  `npc_category` int(11) NOT NULL DEFAULT 0,
  `npc_usefulness` int(11) NOT NULL DEFAULT 0,
  `MinResist` int(11) NOT NULL DEFAULT 0,
  `MaxResist` int(11) NOT NULL DEFAULT 0,
  `viral_targets` int(11) NOT NULL DEFAULT 0,
  `viral_timer` int(11) NOT NULL DEFAULT 0,
  `field193` int(11) NOT NULL DEFAULT 0,
  `ConeStartAngle` int(11) NOT NULL DEFAULT 0,
  `ConeStopAngle` int(11) NOT NULL DEFAULT 0,
  `sneaking` int(11) NOT NULL DEFAULT 0,
  `not_extendable` int(11) NOT NULL DEFAULT 0,
  `field198` int(11) NOT NULL DEFAULT 0,
  `field199` int(11) NOT NULL DEFAULT 1,
  `suspendable` int(11) DEFAULT 0,
  `viral_range` int(11) NOT NULL DEFAULT 0,
  `songcap` int(11) DEFAULT 0,
  `field203` int(11) DEFAULT 0,
  `field204` int(11) DEFAULT 0,
  `no_block` int(11) NOT NULL DEFAULT 0,
  `field206` int(11) DEFAULT -1,
  `spellgroup` int(11) DEFAULT 0,
  `rank` int(11) NOT NULL DEFAULT 0,
  `field209` int(11) DEFAULT 0,
  `field210` int(11) DEFAULT 1,
  `CastRestriction` int(11) NOT NULL DEFAULT 0,
  `field212` int(11) DEFAULT 0,
  `InCombat` int(11) NOT NULL DEFAULT 0,
  `OutofCombat` int(11) NOT NULL DEFAULT 0,
  `field215` int(11) DEFAULT 0,
  `field216` int(11) DEFAULT 0,
  `field217` int(11) DEFAULT 0,
  `aemaxtargets` int(11) NOT NULL DEFAULT 0,
  `maxtargets` int(11) DEFAULT 0,
  `field220` int(11) DEFAULT 0,
  `field221` int(11) DEFAULT 0,
  `field222` int(11) DEFAULT 0,
  `field223` int(11) DEFAULT 0,
  `persistdeath` int(11) DEFAULT 0,
  `field225` int(11) NOT NULL DEFAULT 0,
  `field226` int(11) NOT NULL DEFAULT 0,
  `min_dist` float NOT NULL DEFAULT 0,
  `min_dist_mod` float NOT NULL DEFAULT 0,
  `max_dist` float NOT NULL DEFAULT 0,
  `max_dist_mod` float NOT NULL DEFAULT 0,
  `min_range` int(11) NOT NULL DEFAULT 0,
  `field232` int(11) NOT NULL DEFAULT 0,
  `field233` int(11) NOT NULL DEFAULT 0,
  `field234` int(11) NOT NULL DEFAULT 0,
  `field235` int(11) NOT NULL DEFAULT 0,
  `field236` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `spire_analytic_event_counts`
--

DROP TABLE IF EXISTS `spire_analytic_event_counts`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `spire_analytic_event_counts` (
  `id` bigint(20) unsigned NOT NULL AUTO_INCREMENT,
  `event_name` varchar(50) DEFAULT NULL,
  `event_key` varchar(120) DEFAULT NULL,
  `count` bigint(11) DEFAULT NULL,
  `updated_at` datetime(3) DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `event_name` (`event_name`),
  KEY `event_name_key` (`event_name`,`event_key`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `spire_analytic_events`
--

DROP TABLE IF EXISTS `spire_analytic_events`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `spire_analytic_events` (
  `id` bigint(20) unsigned NOT NULL AUTO_INCREMENT,
  `event_name` varchar(50) DEFAULT NULL,
  `event_value` varchar(200) DEFAULT NULL,
  `request_uri` varchar(250) DEFAULT NULL,
  `ip_address` varchar(20) DEFAULT NULL,
  `user_id` bigint(11) DEFAULT 0,
  `updated_at` datetime(3) DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `event_name_ip_address` (`event_name`,`ip_address`),
  KEY `event_name` (`event_name`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `spire_crash_reports`
--

DROP TABLE IF EXISTS `spire_crash_reports`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `spire_crash_reports` (
  `id` bigint(20) unsigned NOT NULL AUTO_INCREMENT,
  `platform_name` varchar(20) DEFAULT NULL,
  `origination_info` varchar(150) DEFAULT NULL,
  `compile_date` varchar(20) DEFAULT NULL,
  `compile_time` varchar(20) DEFAULT NULL,
  `cpus` bigint(20) DEFAULT NULL,
  `crash_report` longtext DEFAULT NULL,
  `os_machine` varchar(200) DEFAULT NULL,
  `os_release` varchar(200) DEFAULT NULL,
  `os_sysname` varchar(200) DEFAULT NULL,
  `os_version` varchar(200) DEFAULT NULL,
  `process_id` bigint(20) DEFAULT NULL,
  `rss_memory` double DEFAULT NULL,
  `server_name` varchar(200) DEFAULT NULL,
  `server_short_name` varchar(200) DEFAULT NULL,
  `server_version` varchar(50) DEFAULT NULL,
  `fingerprint` varchar(100) DEFAULT NULL,
  `resolved` tinyint(1) DEFAULT 0,
  `resolved_by` bigint(20) unsigned DEFAULT 0,
  `resolved_at` timestamp NULL DEFAULT NULL,
  `uptime` bigint(20) DEFAULT NULL,
  `created_at` datetime(3) DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `version` (`server_version`),
  KEY `fingerprint` (`fingerprint`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `spire_server_database_connections`
--

DROP TABLE IF EXISTS `spire_server_database_connections`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `spire_server_database_connections` (
  `id` bigint(20) unsigned NOT NULL AUTO_INCREMENT,
  `name` varchar(255) DEFAULT NULL,
  `db_host` varchar(50) DEFAULT NULL,
  `db_port` varchar(50) DEFAULT NULL,
  `db_name` varchar(50) DEFAULT NULL,
  `db_username` varchar(50) DEFAULT NULL,
  `db_password` varchar(250) DEFAULT NULL,
  `content_db_host` varchar(50) DEFAULT NULL,
  `content_db_port` varchar(50) DEFAULT NULL,
  `content_db_name` varchar(50) DEFAULT NULL,
  `content_db_username` varchar(50) DEFAULT NULL,
  `content_db_password` varchar(250) DEFAULT NULL,
  `logs_db_host` varchar(50) DEFAULT NULL,
  `logs_db_port` varchar(50) DEFAULT NULL,
  `logs_db_name` varchar(50) DEFAULT NULL,
  `logs_db_username` varchar(50) DEFAULT NULL,
  `logs_db_password` varchar(250) DEFAULT NULL,
  `discord_webhook_url` varchar(250) DEFAULT NULL,
  `created_from_ip` varchar(50) DEFAULT NULL,
  `created_by` bigint(20) unsigned DEFAULT 0,
  `created_at` datetime(3) DEFAULT NULL,
  `updated_at` datetime(3) DEFAULT NULL,
  `deleted_at` datetime(3) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `spire_settings`
--

DROP TABLE IF EXISTS `spire_settings`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `spire_settings` (
  `id` bigint(20) unsigned NOT NULL AUTO_INCREMENT,
  `setting` varchar(190) DEFAULT NULL,
  `value` varchar(255) DEFAULT NULL,
  `created_at` datetime(3) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `idx_spire_settings_setting` (`setting`)
) ENGINE=InnoDB AUTO_INCREMENT=3 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `spire_user_event_log`
--

DROP TABLE IF EXISTS `spire_user_event_log`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `spire_user_event_log` (
  `id` bigint(20) unsigned NOT NULL AUTO_INCREMENT,
  `user_id` bigint(20) unsigned DEFAULT NULL,
  `server_database_connection_id` bigint(20) unsigned DEFAULT NULL,
  `event_name` varchar(191) DEFAULT NULL,
  `data` longtext DEFAULT NULL,
  `created_at` datetime(3) DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `server_database_connection_id_event` (`server_database_connection_id`,`event_name`),
  KEY `server_database_connection_id` (`server_database_connection_id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `spire_user_server_database_connections`
--

DROP TABLE IF EXISTS `spire_user_server_database_connections`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `spire_user_server_database_connections` (
  `id` bigint(20) unsigned NOT NULL AUTO_INCREMENT,
  `user_id` bigint(20) unsigned DEFAULT NULL,
  `server_database_connection_id` bigint(20) unsigned DEFAULT NULL,
  `active` bigint(20) unsigned DEFAULT 0,
  `created_by` bigint(20) unsigned DEFAULT 0,
  `created_at` datetime(3) DEFAULT NULL,
  `updated_at` datetime(3) DEFAULT NULL,
  `deleted_at` datetime(3) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `spire_user_server_resource_permissions`
--

DROP TABLE IF EXISTS `spire_user_server_resource_permissions`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `spire_user_server_resource_permissions` (
  `id` bigint(20) unsigned NOT NULL AUTO_INCREMENT,
  `user_id` bigint(20) unsigned DEFAULT NULL,
  `server_database_connection_id` bigint(20) unsigned DEFAULT NULL,
  `resource_name` longtext DEFAULT NULL,
  `can_write` tinyint(3) unsigned DEFAULT NULL,
  `can_read` tinyint(3) unsigned DEFAULT NULL,
  `created_at` datetime(3) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `spire_users`
--

DROP TABLE IF EXISTS `spire_users`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `spire_users` (
  `id` bigint(20) unsigned NOT NULL AUTO_INCREMENT,
  `user_name` longtext DEFAULT NULL,
  `full_name` longtext DEFAULT NULL,
  `first_name` longtext DEFAULT NULL,
  `last_name` longtext DEFAULT NULL,
  `email` longtext DEFAULT NULL,
  `avatar` longtext DEFAULT NULL,
  `provider` longtext DEFAULT NULL,
  `password` longtext DEFAULT NULL,
  `is_admin` tinyint(1) DEFAULT 0,
  `is_server_developer` tinyint(1) DEFAULT 0,
  `created_at` datetime(3) DEFAULT NULL,
  `updated_at` datetime(3) DEFAULT NULL,
  `deleted` datetime(3) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `start_zones`
--

DROP TABLE IF EXISTS `start_zones`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `start_zones` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `x` float NOT NULL DEFAULT 0,
  `y` float NOT NULL DEFAULT 0,
  `z` float NOT NULL DEFAULT 0,
  `heading` float NOT NULL DEFAULT 0,
  `zone_id` int(4) NOT NULL DEFAULT 0,
  `bind_id` int(4) NOT NULL DEFAULT 0,
  `player_choice` int(2) NOT NULL DEFAULT 0,
  `player_class` int(2) NOT NULL DEFAULT 0,
  `player_deity` int(4) NOT NULL DEFAULT 0,
  `player_race` int(4) NOT NULL DEFAULT 0,
  `start_zone` int(4) NOT NULL DEFAULT 0,
  `bind_x` float NOT NULL DEFAULT 0,
  `bind_y` float NOT NULL DEFAULT 0,
  `bind_z` float NOT NULL DEFAULT 0,
  `select_rank` tinyint(3) unsigned NOT NULL DEFAULT 50,
  `min_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `max_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `content_flags` varchar(100) DEFAULT NULL,
  `content_flags_disabled` varchar(100) DEFAULT NULL,
  PRIMARY KEY (`id`,`player_choice`,`player_race`,`player_class`,`player_deity`)
) ENGINE=MyISAM AUTO_INCREMENT=345 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `starting_items`
--

DROP TABLE IF EXISTS `starting_items`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `starting_items` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `class_list` text DEFAULT NULL,
  `race_list` text DEFAULT NULL,
  `deity_list` text DEFAULT NULL,
  `zone_id_list` text DEFAULT NULL,
  `item_id` int(11) unsigned NOT NULL DEFAULT 0,
  `item_charges` tinyint(3) unsigned NOT NULL DEFAULT 1,
  `augment_one` int(11) unsigned NOT NULL DEFAULT 0,
  `augment_two` int(11) unsigned NOT NULL DEFAULT 0,
  `augment_three` int(11) unsigned NOT NULL DEFAULT 0,
  `augment_four` int(11) unsigned NOT NULL DEFAULT 0,
  `augment_five` int(11) unsigned NOT NULL DEFAULT 0,
  `augment_six` int(11) unsigned NOT NULL DEFAULT 0,
  `status` mediumint(3) NOT NULL DEFAULT 0,
  `inventory_slot` mediumint(9) NOT NULL DEFAULT -1,
  `min_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `max_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `content_flags` varchar(100) DEFAULT NULL,
  `content_flags_disabled` varchar(100) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=150 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `starting_items___`
--

DROP TABLE IF EXISTS `starting_items___`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `starting_items___` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `race` int(11) NOT NULL DEFAULT 0,
  `class` int(11) NOT NULL DEFAULT 0,
  `deityid` int(11) NOT NULL DEFAULT 0,
  `zoneid` int(11) NOT NULL DEFAULT 0,
  `itemid` int(11) NOT NULL DEFAULT 0,
  `item_charges` tinyint(3) unsigned NOT NULL DEFAULT 1,
  `gm` tinyint(1) NOT NULL DEFAULT 0,
  `slot` mediumint(9) NOT NULL DEFAULT -1,
  PRIMARY KEY (`id`,`race`)
) ENGINE=MyISAM AUTO_INCREMENT=182 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `starting_items_backup_9243`
--

DROP TABLE IF EXISTS `starting_items_backup_9243`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `starting_items_backup_9243` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `race` int(11) NOT NULL DEFAULT 0,
  `class` int(11) NOT NULL DEFAULT 0,
  `deityid` int(11) NOT NULL DEFAULT 0,
  `zoneid` int(11) NOT NULL DEFAULT 0,
  `itemid` int(11) NOT NULL DEFAULT 0,
  `item_charges` tinyint(3) unsigned NOT NULL DEFAULT 1,
  `gm` tinyint(1) NOT NULL DEFAULT 0,
  `slot` mediumint(9) NOT NULL DEFAULT -1,
  `min_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `max_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `content_flags` varchar(100) DEFAULT NULL,
  `content_flags_disabled` varchar(100) DEFAULT NULL,
  PRIMARY KEY (`id`,`race`)
) ENGINE=InnoDB AUTO_INCREMENT=246 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `task_activities`
--

DROP TABLE IF EXISTS `task_activities`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `task_activities` (
  `taskid` int(11) unsigned NOT NULL DEFAULT 0,
  `activityid` int(11) unsigned NOT NULL DEFAULT 0,
  `req_activity_id` int(11) NOT NULL DEFAULT -1,
  `step` int(11) NOT NULL DEFAULT 0,
  `activitytype` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `target_name` varchar(64) NOT NULL DEFAULT '',
  `goalmethod` int(10) unsigned NOT NULL DEFAULT 0,
  `goalcount` int(11) DEFAULT 1,
  `description_override` varchar(128) CHARACTER SET latin1 COLLATE latin1_swedish_ci NOT NULL DEFAULT '',
  `npc_match_list` text DEFAULT NULL,
  `item_id_list` text DEFAULT NULL,
  `item_list` varchar(128) CHARACTER SET latin1 COLLATE latin1_swedish_ci NOT NULL DEFAULT '',
  `dz_switch_id` int(11) NOT NULL DEFAULT 0,
  `min_x` float NOT NULL DEFAULT 0,
  `min_y` float NOT NULL DEFAULT 0,
  `min_z` float NOT NULL DEFAULT 0,
  `max_x` float NOT NULL DEFAULT 0,
  `max_y` float NOT NULL DEFAULT 0,
  `max_z` float NOT NULL DEFAULT 0,
  `skill_list` varchar(64) CHARACTER SET latin1 COLLATE latin1_swedish_ci NOT NULL DEFAULT '-1',
  `spell_list` varchar(64) CHARACTER SET latin1 COLLATE latin1_swedish_ci NOT NULL DEFAULT '0',
  `zones` varchar(64) NOT NULL DEFAULT '',
  `zone_version` int(11) DEFAULT -1,
  `optional` tinyint(1) NOT NULL DEFAULT 0,
  `list_group` tinyint(3) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`taskid`,`activityid`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `task_activities_backup_9203`
--

DROP TABLE IF EXISTS `task_activities_backup_9203`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `task_activities_backup_9203` (
  `taskid` int(11) unsigned NOT NULL DEFAULT 0,
  `activityid` int(11) unsigned NOT NULL DEFAULT 0,
  `req_activity_id` int(11) NOT NULL DEFAULT -1,
  `step` int(11) NOT NULL DEFAULT 0,
  `activitytype` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `target_name` varchar(64) NOT NULL DEFAULT '',
  `item_list` varchar(128) NOT NULL DEFAULT '',
  `skill_list` varchar(64) NOT NULL DEFAULT '-1',
  `spell_list` varchar(64) NOT NULL DEFAULT '0',
  `description_override` varchar(128) NOT NULL DEFAULT '',
  `goalid` int(11) unsigned NOT NULL DEFAULT 0,
  `goal_match_list` text DEFAULT NULL,
  `goalmethod` int(10) unsigned NOT NULL DEFAULT 0,
  `goalcount` int(11) DEFAULT 1,
  `delivertonpc` int(11) unsigned NOT NULL DEFAULT 0,
  `zones` varchar(64) NOT NULL DEFAULT '',
  `zone_version` int(11) DEFAULT -1,
  `optional` tinyint(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`taskid`,`activityid`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `tasks`
--

DROP TABLE IF EXISTS `tasks`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `tasks` (
  `id` int(11) unsigned NOT NULL DEFAULT 0,
  `type` tinyint(4) NOT NULL DEFAULT 0,
  `duration` int(11) unsigned NOT NULL DEFAULT 0,
  `duration_code` tinyint(4) NOT NULL DEFAULT 0,
  `title` varchar(100) NOT NULL DEFAULT '',
  `description` text NOT NULL,
  `reward_text` varchar(64) CHARACTER SET latin1 COLLATE latin1_swedish_ci NOT NULL DEFAULT '',
  `reward_id_list` text CHARACTER SET latin1 COLLATE latin1_swedish_ci DEFAULT NULL,
  `cash_reward` int(11) unsigned NOT NULL DEFAULT 0,
  `exp_reward` int(10) NOT NULL DEFAULT 0,
  `reward_method` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `reward_points` int(11) NOT NULL DEFAULT 0,
  `reward_point_type` int(11) NOT NULL DEFAULT 0,
  `min_level` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `max_level` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `level_spread` int(10) unsigned NOT NULL DEFAULT 0,
  `min_players` int(10) unsigned NOT NULL DEFAULT 0,
  `max_players` int(10) unsigned NOT NULL DEFAULT 0,
  `repeatable` tinyint(1) unsigned NOT NULL DEFAULT 1,
  `faction_reward` int(10) NOT NULL DEFAULT 0,
  `completion_emote` varchar(512) CHARACTER SET latin1 COLLATE latin1_swedish_ci NOT NULL DEFAULT '',
  `replay_timer_group` int(10) unsigned NOT NULL DEFAULT 0,
  `replay_timer_seconds` int(10) unsigned NOT NULL DEFAULT 0,
  `request_timer_group` int(10) unsigned NOT NULL DEFAULT 0,
  `request_timer_seconds` int(10) unsigned NOT NULL DEFAULT 0,
  `dz_template_id` int(10) unsigned NOT NULL DEFAULT 0,
  `lock_activity_id` int(11) NOT NULL DEFAULT -1,
  `faction_amount` int(10) NOT NULL DEFAULT 0,
  `enabled` smallint(6) DEFAULT 1,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `tasks_backup_9_25_2022`
--

DROP TABLE IF EXISTS `tasks_backup_9_25_2022`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `tasks_backup_9_25_2022` (
  `id` int(11) unsigned NOT NULL DEFAULT 0,
  `type` tinyint(4) NOT NULL DEFAULT 0,
  `duration` int(11) unsigned NOT NULL DEFAULT 0,
  `duration_code` tinyint(4) NOT NULL DEFAULT 0,
  `title` varchar(100) NOT NULL DEFAULT '',
  `description` text NOT NULL,
  `reward` varchar(64) NOT NULL DEFAULT '',
  `rewardid` int(11) unsigned NOT NULL DEFAULT 0,
  `cashreward` int(11) unsigned NOT NULL DEFAULT 0,
  `xpreward` int(10) unsigned NOT NULL DEFAULT 0,
  `rewardmethod` tinyint(3) unsigned NOT NULL DEFAULT 2,
  `reward_points` int(11) NOT NULL DEFAULT 0,
  `reward_point_type` int(11) NOT NULL DEFAULT 0,
  `minlevel` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `maxlevel` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `level_spread` int(10) unsigned NOT NULL DEFAULT 0,
  `min_players` int(10) unsigned NOT NULL DEFAULT 0,
  `max_players` int(10) unsigned NOT NULL DEFAULT 0,
  `repeatable` tinyint(1) unsigned NOT NULL DEFAULT 1,
  `faction_reward` int(10) NOT NULL DEFAULT 0,
  `completion_emote` varchar(512) CHARACTER SET latin1 COLLATE latin1_swedish_ci NOT NULL DEFAULT '',
  `replay_timer_group` int(10) unsigned NOT NULL DEFAULT 0,
  `replay_timer_seconds` int(10) unsigned NOT NULL DEFAULT 0,
  `request_timer_group` int(10) unsigned NOT NULL DEFAULT 0,
  `request_timer_seconds` int(10) unsigned NOT NULL DEFAULT 0,
  `dz_template_id` int(10) unsigned NOT NULL DEFAULT 0,
  `lock_activity_id` int(11) NOT NULL DEFAULT -1,
  `faction_amount` int(10) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `tasksets`
--

DROP TABLE IF EXISTS `tasksets`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `tasksets` (
  `id` int(11) unsigned NOT NULL DEFAULT 0,
  `taskid` int(11) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`,`taskid`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `taunt_flags`
--

DROP TABLE IF EXISTS `taunt_flags`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `taunt_flags` (
  `charID` int(11) NOT NULL DEFAULT 0,
  `tauntID` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`charID`)
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `timers`
--

DROP TABLE IF EXISTS `timers`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `timers` (
  `char_id` int(11) NOT NULL DEFAULT 0,
  `type` mediumint(8) unsigned NOT NULL DEFAULT 0,
  `start` int(10) unsigned NOT NULL DEFAULT 0,
  `duration` int(10) unsigned NOT NULL DEFAULT 0,
  `enable` tinyint(4) NOT NULL DEFAULT 0,
  PRIMARY KEY (`char_id`,`type`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `titles`
--

DROP TABLE IF EXISTS `titles`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `titles` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `skill_id` tinyint(3) NOT NULL DEFAULT -1,
  `min_skill_value` mediumint(8) NOT NULL DEFAULT -1,
  `max_skill_value` mediumint(8) NOT NULL DEFAULT -1,
  `min_aa_points` mediumint(8) NOT NULL DEFAULT -1,
  `max_aa_points` mediumint(8) NOT NULL DEFAULT -1,
  `class` tinyint(4) NOT NULL DEFAULT -1,
  `gender` tinyint(1) NOT NULL DEFAULT -1 COMMENT '-1 = either, 0 = male, 1 = female',
  `char_id` int(11) NOT NULL DEFAULT -1,
  `status` int(11) NOT NULL DEFAULT -1,
  `item_id` int(11) NOT NULL DEFAULT -1,
  `prefix` varchar(32) NOT NULL DEFAULT '',
  `suffix` varchar(32) NOT NULL DEFAULT '',
  `title_set` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `tool_game_objects`
--

DROP TABLE IF EXISTS `tool_game_objects`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `tool_game_objects` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `zoneid` int(11) NOT NULL DEFAULT 0,
  `zonesn` varchar(50) NOT NULL DEFAULT '',
  `object_name` varchar(50) NOT NULL DEFAULT '',
  `file_from` varchar(50) DEFAULT NULL,
  `is_global` tinyint(1) DEFAULT 0,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=51381 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `trader`
--

DROP TABLE IF EXISTS `trader`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `trader` (
  `id` bigint(20) unsigned NOT NULL AUTO_INCREMENT,
  `char_id` int(11) unsigned NOT NULL DEFAULT 0,
  `item_id` int(11) unsigned NOT NULL DEFAULT 0,
  `aug_slot_1` int(10) unsigned NOT NULL DEFAULT 0,
  `aug_slot_2` int(10) unsigned NOT NULL DEFAULT 0,
  `aug_slot_3` int(10) unsigned NOT NULL DEFAULT 0,
  `aug_slot_4` int(10) unsigned NOT NULL DEFAULT 0,
  `aug_slot_5` int(10) unsigned NOT NULL DEFAULT 0,
  `aug_slot_6` int(10) unsigned NOT NULL DEFAULT 0,
  `item_sn` int(10) unsigned NOT NULL DEFAULT 0,
  `item_charges` int(11) NOT NULL DEFAULT 0,
  `item_cost` int(10) unsigned NOT NULL DEFAULT 0,
  `slot_id` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `char_entity_id` int(11) unsigned NOT NULL DEFAULT 0,
  `char_zone_id` int(11) unsigned NOT NULL DEFAULT 0,
  `active_transaction` tinyint(3) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `charid_slotid` (`char_id`,`slot_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `trader_audit`
--

DROP TABLE IF EXISTS `trader_audit`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `trader_audit` (
  `time` datetime NOT NULL DEFAULT '0000-00-00 00:00:00',
  `seller` varchar(64) NOT NULL DEFAULT '',
  `buyer` varchar(64) NOT NULL DEFAULT '',
  `itemname` varchar(64) NOT NULL DEFAULT '',
  `quantity` int(11) NOT NULL DEFAULT 0,
  `totalcost` int(11) NOT NULL DEFAULT 0,
  `trantype` tinyint(4) NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `tradeskill_recipe`
--

DROP TABLE IF EXISTS `tradeskill_recipe`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `tradeskill_recipe` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(64) NOT NULL DEFAULT '',
  `tradeskill` smallint(6) NOT NULL DEFAULT 0,
  `skillneeded` smallint(6) NOT NULL DEFAULT 0,
  `trivial` smallint(6) NOT NULL DEFAULT 0,
  `nofail` tinyint(1) NOT NULL DEFAULT 0,
  `replace_container` tinyint(1) NOT NULL DEFAULT 0,
  `notes` tinytext DEFAULT NULL,
  `enabled` tinyint(1) NOT NULL DEFAULT 1,
  `must_learn` tinyint(4) NOT NULL DEFAULT 0,
  `learned_by_item_id` int(11) NOT NULL DEFAULT 0,
  `quest` tinyint(1) NOT NULL DEFAULT 0,
  `min_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `max_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `content_flags` varchar(100) DEFAULT NULL,
  `content_flags_disabled` varchar(100) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=10115 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `tradeskill_recipe_entries`
--

DROP TABLE IF EXISTS `tradeskill_recipe_entries`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `tradeskill_recipe_entries` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `recipe_id` int(11) NOT NULL DEFAULT 0,
  `item_id` int(11) NOT NULL DEFAULT 0,
  `successcount` tinyint(2) NOT NULL DEFAULT 0,
  `failcount` tinyint(2) NOT NULL DEFAULT 0,
  `componentcount` tinyint(2) NOT NULL DEFAULT 1,
  `salvagecount` tinyint(2) NOT NULL DEFAULT 0,
  `iscontainer` tinyint(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `recipe_id` (`recipe_id`),
  KEY `item_id` (`item_id`)
) ENGINE=InnoDB AUTO_INCREMENT=126516 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `traps`
--

DROP TABLE IF EXISTS `traps`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `traps` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `zone` varchar(16) NOT NULL DEFAULT '',
  `version` smallint(5) unsigned NOT NULL DEFAULT 0,
  `x` int(11) NOT NULL DEFAULT 0,
  `y` int(11) NOT NULL DEFAULT 0,
  `z` int(11) NOT NULL DEFAULT 0,
  `chance` tinyint(4) NOT NULL DEFAULT 0,
  `maxzdiff` float NOT NULL DEFAULT 0,
  `radius` float NOT NULL DEFAULT 0,
  `effect` int(11) NOT NULL DEFAULT 0,
  `effectvalue` int(11) NOT NULL DEFAULT 0,
  `effectvalue2` int(11) NOT NULL DEFAULT 0,
  `message` varchar(200) NOT NULL DEFAULT '',
  `skill` int(11) NOT NULL DEFAULT 0,
  `level` mediumint(4) unsigned NOT NULL DEFAULT 1,
  `respawn_time` int(11) unsigned NOT NULL DEFAULT 60,
  `respawn_var` int(11) unsigned NOT NULL DEFAULT 0,
  `triggered_number` tinyint(4) NOT NULL DEFAULT 0,
  `group` tinyint(4) NOT NULL DEFAULT 0,
  `despawn_when_triggered` tinyint(4) NOT NULL DEFAULT 0,
  `undetectable` tinyint(4) NOT NULL DEFAULT 0,
  `min_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `max_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `content_flags` varchar(100) DEFAULT NULL,
  `content_flags_disabled` varchar(100) DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `zone` (`zone`)
) ENGINE=MyISAM AUTO_INCREMENT=891 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `tribute_levels`
--

DROP TABLE IF EXISTS `tribute_levels`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `tribute_levels` (
  `tribute_id` int(10) unsigned NOT NULL DEFAULT 0,
  `level` int(10) unsigned NOT NULL DEFAULT 0,
  `cost` int(10) unsigned NOT NULL DEFAULT 0,
  `item_id` int(10) unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (`tribute_id`,`level`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `tributes`
--

DROP TABLE IF EXISTS `tributes`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `tributes` (
  `id` int(10) unsigned NOT NULL DEFAULT 0,
  `unknown` int(10) unsigned NOT NULL DEFAULT 0,
  `name` varchar(255) NOT NULL DEFAULT '',
  `descr` mediumtext NOT NULL,
  `isguild` tinyint(4) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`,`isguild`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Temporary table structure for view `vaggroradius`
--

DROP TABLE IF EXISTS `vaggroradius`;
/*!50001 DROP VIEW IF EXISTS `vaggroradius`*/;
SET @saved_cs_client     = @@character_set_client;
SET character_set_client = utf8mb4;
/*!50001 CREATE VIEW `vaggroradius` AS SELECT
 1 AS `id`,
  1 AS `name`,
  1 AS `aggroradius`,
  1 AS `assistradius`,
  1 AS `Spawn2X`,
  1 AS `Spawn2Y`,
  1 AS `Spawn2Z`,
  1 AS `spawngroup_name`,
  1 AS `Spawngroup_id`,
  1 AS `Spawngroup_minX`,
  1 AS `Spawngroup_maxX`,
  1 AS `Spawngroup_minY`,
  1 AS `Spawngroup_maxY`,
  1 AS `Spawngroup_dist`,
  1 AS `Spawngroup_mindelay`,
  1 AS `Spawngroup_delay` */;
SET character_set_client = @saved_cs_client;

--
-- Table structure for table `variables`
--

DROP TABLE IF EXISTS `variables`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `variables` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `varname` varchar(25) NOT NULL DEFAULT '',
  `value` text NOT NULL,
  `information` text NOT NULL,
  `ts` timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE KEY `varname` (`varname`)
) ENGINE=InnoDB AUTO_INCREMENT=34 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Temporary table structure for view `vatkdelayzero`
--

DROP TABLE IF EXISTS `vatkdelayzero`;
/*!50001 DROP VIEW IF EXISTS `vatkdelayzero`*/;
SET @saved_cs_client     = @@character_set_client;
SET character_set_client = utf8mb4;
/*!50001 CREATE VIEW `vatkdelayzero` AS SELECT
 1 AS `id`,
  1 AS `name`,
  1 AS `level`,
  1 AS `attack_delay`,
  1 AS `zone` */;
SET character_set_client = @saved_cs_client;

--
-- Temporary table structure for view `vbot_spells`
--

DROP TABLE IF EXISTS `vbot_spells`;
/*!50001 DROP VIEW IF EXISTS `vbot_spells`*/;
SET @saved_cs_client     = @@character_set_client;
SET character_set_client = utf8mb4;
/*!50001 CREATE VIEW `vbot_spells` AS SELECT
 1 AS `spell_set_name`,
  1 AS `spell_name`,
  1 AS `spellid`,
  1 AS `type`,
  1 AS `minlevel`,
  1 AS `maxlevel`,
  1 AS `manacost`,
  1 AS `recast_delay`,
  1 AS `priority`,
  1 AS `resist_adjust`,
  1 AS `min_hp`,
  1 AS `max_hp`,
  1 AS `bucket_name`,
  1 AS `bucket_value`,
  1 AS `bucket_comparison` */;
SET character_set_client = @saved_cs_client;

--
-- Temporary table structure for view `vbotsnpcspells`
--

DROP TABLE IF EXISTS `vbotsnpcspells`;
/*!50001 DROP VIEW IF EXISTS `vbotsnpcspells`*/;
SET @saved_cs_client     = @@character_set_client;
SET character_set_client = utf8mb4;
/*!50001 CREATE VIEW `vbotsnpcspells` AS SELECT
 1 AS `bse_spellid`,
  1 AS `npc_spells_id`,
  1 AS `name`,
  1 AS `spellid`,
  1 AS `type`,
  1 AS `minlevel`,
  1 AS `maxlevel`,
  1 AS `manacost`,
  1 AS `recast_delay`,
  1 AS `priority`,
  1 AS `resist_adjust` */;
SET character_set_client = @saved_cs_client;

--
-- Temporary table structure for view `vemotes`
--

DROP TABLE IF EXISTS `vemotes`;
/*!50001 DROP VIEW IF EXISTS `vemotes`*/;
SET @saved_cs_client     = @@character_set_client;
SET character_set_client = utf8mb4;
/*!50001 CREATE VIEW `vemotes` AS SELECT
 1 AS `id`,
  1 AS `name`,
  1 AS `emoteid`,
  1 AS `Emote_Event`,
  1 AS `Event_Type`,
  1 AS `text`,
  1 AS `Spawn2X`,
  1 AS `Spawn2Y`,
  1 AS `Spawn2Z`,
  1 AS `spawngroup_name`,
  1 AS `Spawngroup_id`,
  1 AS `Spawngroup_minX`,
  1 AS `Spawngroup_maxX`,
  1 AS `Spawngroup_minY`,
  1 AS `Spawngroup_maxY`,
  1 AS `Spawngroup_dist`,
  1 AS `Spawngroup_mindelay`,
  1 AS `Spawngroup_delay` */;
SET character_set_client = @saved_cs_client;

--
-- Table structure for table `veteran_reward_templates`
--

DROP TABLE IF EXISTS `veteran_reward_templates`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `veteran_reward_templates` (
  `claim_id` int(10) unsigned NOT NULL DEFAULT 0,
  `name` varchar(64) NOT NULL DEFAULT '',
  `item_id` int(10) unsigned NOT NULL DEFAULT 0,
  `charges` smallint(5) unsigned NOT NULL DEFAULT 0,
  `reward_slot` tinyint(3) unsigned NOT NULL DEFAULT 0,
  UNIQUE KEY `claim_reward` (`claim_id`,`reward_slot`),
  KEY `claim_id` (`claim_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Temporary table structure for view `vfaction`
--

DROP TABLE IF EXISTS `vfaction`;
/*!50001 DROP VIEW IF EXISTS `vfaction`*/;
SET @saved_cs_client     = @@character_set_client;
SET character_set_client = utf8mb4;
/*!50001 CREATE VIEW `vfaction` AS SELECT
 1 AS `id`,
  1 AS `sgID`,
  1 AS `name`,
  1 AS `npc_faction_id` */;
SET character_set_client = @saved_cs_client;

--
-- Temporary table structure for view `vhp`
--

DROP TABLE IF EXISTS `vhp`;
/*!50001 DROP VIEW IF EXISTS `vhp`*/;
SET @saved_cs_client     = @@character_set_client;
SET character_set_client = utf8mb4;
/*!50001 CREATE VIEW `vhp` AS SELECT
 1 AS `id`,
  1 AS `name`,
  1 AS `level`,
  1 AS `hp`,
  1 AS `hp_regen_rate` */;
SET character_set_client = @saved_cs_client;

--
-- Temporary table structure for view `vloottables`
--

DROP TABLE IF EXISTS `vloottables`;
/*!50001 DROP VIEW IF EXISTS `vloottables`*/;
SET @saved_cs_client     = @@character_set_client;
SET character_set_client = utf8mb4;
/*!50001 CREATE VIEW `vloottables` AS SELECT
 1 AS `id`,
  1 AS `name`,
  1 AS `level`,
  1 AS `loottable_id` */;
SET character_set_client = @saved_cs_client;

--
-- Temporary table structure for view `vrespawn`
--

DROP TABLE IF EXISTS `vrespawn`;
/*!50001 DROP VIEW IF EXISTS `vrespawn`*/;
SET @saved_cs_client     = @@character_set_client;
SET character_set_client = utf8mb4;
/*!50001 CREATE VIEW `vrespawn` AS SELECT
 1 AS `id`,
  1 AS `name`,
  1 AS `level`,
  1 AS `respawntime`,
  1 AS `Spawn2X`,
  1 AS `Spawn2Y`,
  1 AS `Spawn2Z`,
  1 AS `spawngroup_name`,
  1 AS `Spawngroup_id`,
  1 AS `Spawngroup_minX`,
  1 AS `Spawngroup_maxX`,
  1 AS `Spawngroup_minY`,
  1 AS `Spawngroup_maxY`,
  1 AS `Spawngroup_dist`,
  1 AS `Spawngroup_mindelay`,
  1 AS `Spawngroup_delay` */;
SET character_set_client = @saved_cs_client;

--
-- Temporary table structure for view `vspawncondi`
--

DROP TABLE IF EXISTS `vspawncondi`;
/*!50001 DROP VIEW IF EXISTS `vspawncondi`*/;
SET @saved_cs_client     = @@character_set_client;
SET character_set_client = utf8mb4;
/*!50001 CREATE VIEW `vspawncondi` AS SELECT
 1 AS `id`,
  1 AS `name`,
  1 AS `level`,
  1 AS `zone`,
  1 AS `spawngroup_name`,
  1 AS `Spawngroup_id`,
  1 AS `_condition` */;
SET character_set_client = @saved_cs_client;

--
-- Table structure for table `vspawnlocs`
--

DROP TABLE IF EXISTS `vspawnlocs`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `vspawnlocs` (
  `name` text CHARACTER SET utf8mb3 COLLATE utf8mb3_uca1400_ai_ci NOT NULL,
  `npc_faction_id` int(11) NOT NULL,
  `zone` varchar(32) CHARACTER SET utf8mb3 COLLATE utf8mb3_uca1400_ai_ci DEFAULT NULL,
  `Spawn2X` float(14,6) NOT NULL,
  `Spawn2Y` float(14,6) NOT NULL,
  `Spawn2Z` float(14,6) NOT NULL,
  `spawngroup_name` varchar(30) CHARACTER SET utf8mb3 COLLATE utf8mb3_uca1400_ai_ci NOT NULL,
  `Spawngroup_id` int(11) NOT NULL,
  `Spawngroup_minX` float NOT NULL,
  `Spawngroup_maxX` float NOT NULL,
  `Spawngroup_minY` float NOT NULL,
  `Spawngroup_maxY` float NOT NULL,
  `Spawngroup_dist` float NOT NULL,
  `Spawngroup_mindelay` int(11) NOT NULL,
  `Spawngroup_delay` int(11) NOT NULL
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Temporary table structure for view `vspawnlocs2`
--

DROP TABLE IF EXISTS `vspawnlocs2`;
/*!50001 DROP VIEW IF EXISTS `vspawnlocs2`*/;
SET @saved_cs_client     = @@character_set_client;
SET character_set_client = utf8mb4;
/*!50001 CREATE VIEW `vspawnlocs2` AS SELECT
 1 AS `id`,
  1 AS `name`,
  1 AS `CanAggro`,
  1 AS `_condition`,
  1 AS `zone`,
  1 AS `pathgrid`,
  1 AS `Spawn2X`,
  1 AS `Spawn2Y`,
  1 AS `Spawn2Z`,
  1 AS `Spawn2H`,
  1 AS `Respawn`,
  1 AS `spawngroup_name`,
  1 AS `Spawngroup_id`,
  1 AS `Spawngroup_minX`,
  1 AS `Spawngroup_maxX`,
  1 AS `Spawngroup_minY`,
  1 AS `Spawngroup_maxY`,
  1 AS `Spawngroup_dist` */;
SET character_set_client = @saved_cs_client;

--
-- Temporary table structure for view `vspecialattacks`
--

DROP TABLE IF EXISTS `vspecialattacks`;
/*!50001 DROP VIEW IF EXISTS `vspecialattacks`*/;
SET @saved_cs_client     = @@character_set_client;
SET character_set_client = utf8mb4;
/*!50001 CREATE VIEW `vspecialattacks` AS SELECT
 1 AS `id`,
  1 AS `name`,
  1 AS `level`,
  1 AS `hp`,
  1 AS `special_abilities`,
  1 AS `hp_regen_rate`,
  1 AS `mana_regen_rate`,
  1 AS `aggroradius`,
  1 AS `assistradius`,
  1 AS `see_invis`,
  1 AS `see_invis_undead`,
  1 AS `see_hide`,
  1 AS `emoteid`,
  1 AS `attack_count`,
  1 AS `Spawn2X`,
  1 AS `Spawn2Y`,
  1 AS `Spawn2Z`,
  1 AS `spawngroup_name`,
  1 AS `Spawngroup_id`,
  1 AS `Spawngroup_minX`,
  1 AS `Spawngroup_maxX`,
  1 AS `Spawngroup_minY`,
  1 AS `Spawngroup_maxY`,
  1 AS `Spawngroup_dist`,
  1 AS `Spawngroup_mindelay`,
  1 AS `Spawngroup_delay` */;
SET character_set_client = @saved_cs_client;

--
-- Temporary table structure for view `vspellsets`
--

DROP TABLE IF EXISTS `vspellsets`;
/*!50001 DROP VIEW IF EXISTS `vspellsets`*/;
SET @saved_cs_client     = @@character_set_client;
SET character_set_client = utf8mb4;
/*!50001 CREATE VIEW `vspellsets` AS SELECT
 1 AS `id`,
  1 AS `name`,
  1 AS `class`,
  1 AS `npc_spells_id`,
  1 AS `level`,
  1 AS `Spawn2X`,
  1 AS `Spawn2Y`,
  1 AS `Spawn2Z`,
  1 AS `spawngroup_name`,
  1 AS `Spawngroup_id`,
  1 AS `Spawngroup_minX`,
  1 AS `Spawngroup_maxX`,
  1 AS `Spawngroup_minY`,
  1 AS `Spawngroup_maxY`,
  1 AS `Spawngroup_dist`,
  1 AS `Spawngroup_mindelay`,
  1 AS `Spawngroup_delay` */;
SET character_set_client = @saved_cs_client;

--
-- Table structure for table `zone`
--

DROP TABLE IF EXISTS `zone`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `zone` (
  `id` int(10) NOT NULL AUTO_INCREMENT,
  `zoneidnumber` int(4) NOT NULL DEFAULT 0,
  `version` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `short_name` varchar(32) DEFAULT NULL,
  `long_name` text NOT NULL,
  `min_status` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `map_file_name` varchar(100) DEFAULT NULL,
  `note` varchar(200) DEFAULT NULL,
  `min_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `max_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `content_flags` varchar(100) DEFAULT NULL,
  `content_flags_disabled` varchar(100) DEFAULT NULL,
  `expansion` tinyint(3) NOT NULL DEFAULT 0,
  `file_name` varchar(16) DEFAULT NULL,
  `safe_x` float NOT NULL DEFAULT 0,
  `safe_y` float NOT NULL DEFAULT 0,
  `safe_z` float NOT NULL DEFAULT 0,
  `safe_heading` float NOT NULL DEFAULT 0,
  `graveyard_id` float NOT NULL DEFAULT 0,
  `min_level` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `max_level` tinyint(3) unsigned NOT NULL DEFAULT 255,
  `timezone` int(5) NOT NULL DEFAULT 0,
  `maxclients` int(5) NOT NULL DEFAULT 0,
  `ruleset` int(10) unsigned NOT NULL DEFAULT 0,
  `underworld` float NOT NULL DEFAULT 0,
  `minclip` float NOT NULL DEFAULT 450,
  `maxclip` float NOT NULL DEFAULT 450,
  `fog_minclip` float NOT NULL DEFAULT 450,
  `fog_maxclip` float NOT NULL DEFAULT 450,
  `fog_blue` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `fog_red` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `fog_green` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `sky` tinyint(3) unsigned NOT NULL DEFAULT 1,
  `ztype` tinyint(3) unsigned NOT NULL DEFAULT 1,
  `zone_exp_multiplier` decimal(6,2) NOT NULL DEFAULT 0.00,
  `walkspeed` float NOT NULL DEFAULT 0.4,
  `time_type` tinyint(3) unsigned NOT NULL DEFAULT 2,
  `fog_red1` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `fog_green1` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `fog_blue1` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `fog_minclip1` float NOT NULL DEFAULT 450,
  `fog_maxclip1` float NOT NULL DEFAULT 450,
  `fog_red2` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `fog_green2` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `fog_blue2` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `fog_minclip2` float NOT NULL DEFAULT 450,
  `fog_maxclip2` float NOT NULL DEFAULT 450,
  `fog_red3` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `fog_green3` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `fog_blue3` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `fog_minclip3` float NOT NULL DEFAULT 450,
  `fog_maxclip3` float NOT NULL DEFAULT 450,
  `fog_red4` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `fog_green4` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `fog_blue4` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `fog_minclip4` float NOT NULL DEFAULT 450,
  `fog_maxclip4` float NOT NULL DEFAULT 450,
  `fog_density` float NOT NULL DEFAULT 0,
  `flag_needed` varchar(128) NOT NULL DEFAULT '',
  `canbind` tinyint(4) NOT NULL DEFAULT 1,
  `cancombat` tinyint(4) NOT NULL DEFAULT 1,
  `canlevitate` tinyint(4) NOT NULL DEFAULT 1,
  `castoutdoor` tinyint(4) NOT NULL DEFAULT 1,
  `hotzone` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `insttype` tinyint(1) unsigned zerofill NOT NULL DEFAULT 0,
  `shutdowndelay` bigint(16) unsigned NOT NULL DEFAULT 5000,
  `peqzone` tinyint(4) NOT NULL DEFAULT 1,
  `bypass_expansion_check` tinyint(3) NOT NULL DEFAULT 0,
  `suspendbuffs` tinyint(1) unsigned NOT NULL DEFAULT 0,
  `rain_chance1` int(4) NOT NULL DEFAULT 0,
  `rain_chance2` int(4) NOT NULL DEFAULT 0,
  `rain_chance3` int(4) NOT NULL DEFAULT 0,
  `rain_chance4` int(4) NOT NULL DEFAULT 0,
  `rain_duration1` int(4) NOT NULL DEFAULT 0,
  `rain_duration2` int(4) NOT NULL DEFAULT 0,
  `rain_duration3` int(4) NOT NULL DEFAULT 0,
  `rain_duration4` int(4) NOT NULL DEFAULT 0,
  `snow_chance1` int(4) NOT NULL DEFAULT 0,
  `snow_chance2` int(4) NOT NULL DEFAULT 0,
  `snow_chance3` int(4) NOT NULL DEFAULT 0,
  `snow_chance4` int(4) NOT NULL DEFAULT 0,
  `snow_duration1` int(4) NOT NULL DEFAULT 0,
  `snow_duration2` int(4) NOT NULL DEFAULT 0,
  `snow_duration3` int(4) NOT NULL DEFAULT 0,
  `snow_duration4` int(4) NOT NULL DEFAULT 0,
  `gravity` float NOT NULL DEFAULT 0.4,
  `type` int(3) NOT NULL DEFAULT 0,
  `skylock` tinyint(4) NOT NULL DEFAULT 0,
  `fast_regen_hp` int(11) NOT NULL DEFAULT 180,
  `fast_regen_mana` int(11) NOT NULL DEFAULT 180,
  `fast_regen_endurance` int(11) NOT NULL DEFAULT 180,
  `npc_max_aggro_dist` int(11) NOT NULL DEFAULT 600,
  `max_movement_update_range` int(11) unsigned NOT NULL DEFAULT 600,
  `underworld_teleport_index` int(4) NOT NULL DEFAULT 0,
  `lava_damage` int(11) DEFAULT 50,
  `min_lava_damage` int(11) NOT NULL DEFAULT 10,
  `idle_when_empty` tinyint(1) unsigned NOT NULL DEFAULT 1,
  `seconds_before_idle` int(11) unsigned NOT NULL DEFAULT 60,
  PRIMARY KEY (`id`),
  KEY `zoneidnumber` (`zoneidnumber`),
  KEY `zonename` (`short_name`)
) ENGINE=InnoDB AUTO_INCREMENT=5895 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `zone___`
--

DROP TABLE IF EXISTS `zone___`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `zone___` (
  `short_name` varchar(32) NOT NULL,
  `file_name` varchar(16) DEFAULT NULL,
  `long_name` text NOT NULL,
  `map_file_name` varchar(100) DEFAULT NULL,
  `safe_x` float NOT NULL DEFAULT 0,
  `safe_y` float NOT NULL DEFAULT 0,
  `safe_z` float NOT NULL DEFAULT 0,
  `graveyard_id` float NOT NULL DEFAULT 0,
  `min_level` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `min_status` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `zoneidnumber` int(4) NOT NULL DEFAULT 0,
  `version` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `timezone` int(5) NOT NULL DEFAULT 0,
  `maxclients` int(5) NOT NULL DEFAULT 0,
  `ruleset` int(10) unsigned NOT NULL DEFAULT 0,
  `note` varchar(80) DEFAULT NULL,
  `underworld` float NOT NULL DEFAULT 0,
  `minclip` float NOT NULL DEFAULT 450,
  `maxclip` float NOT NULL DEFAULT 450,
  `fog_minclip` float NOT NULL DEFAULT 450,
  `fog_maxclip` float NOT NULL DEFAULT 450,
  `fog_blue` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `fog_red` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `fog_green` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `sky` tinyint(3) unsigned NOT NULL DEFAULT 1,
  `ztype` tinyint(3) unsigned NOT NULL DEFAULT 1,
  `zone_exp_multiplier` decimal(6,2) NOT NULL DEFAULT 0.00,
  `walkspeed` float NOT NULL DEFAULT 0.4,
  `time_type` tinyint(3) unsigned NOT NULL DEFAULT 2,
  `fog_red1` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `fog_green1` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `fog_blue1` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `fog_minclip1` float NOT NULL DEFAULT 450,
  `fog_maxclip1` float NOT NULL DEFAULT 450,
  `fog_red2` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `fog_green2` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `fog_blue2` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `fog_minclip2` float NOT NULL DEFAULT 450,
  `fog_maxclip2` float NOT NULL DEFAULT 450,
  `fog_red3` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `fog_green3` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `fog_blue3` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `fog_minclip3` float NOT NULL DEFAULT 450,
  `fog_maxclip3` float NOT NULL DEFAULT 450,
  `fog_red4` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `fog_green4` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `fog_blue4` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `fog_minclip4` float NOT NULL DEFAULT 450,
  `fog_maxclip4` float NOT NULL DEFAULT 450,
  `fog_density` float NOT NULL DEFAULT 0,
  `flag_needed` varchar(128) NOT NULL DEFAULT '',
  `canbind` tinyint(4) NOT NULL DEFAULT 1,
  `cancombat` tinyint(4) NOT NULL DEFAULT 1,
  `canlevitate` tinyint(4) NOT NULL DEFAULT 1,
  `castoutdoor` tinyint(4) NOT NULL DEFAULT 1,
  `hotzone` tinyint(3) unsigned NOT NULL DEFAULT 0,
  `insttype` tinyint(1) unsigned zerofill NOT NULL DEFAULT 0,
  `shutdowndelay` bigint(16) unsigned NOT NULL DEFAULT 5000,
  `peqzone` tinyint(4) NOT NULL DEFAULT 1,
  `expansion` tinyint(3) NOT NULL DEFAULT 1,
  `suspendbuffs` tinyint(1) unsigned NOT NULL DEFAULT 1,
  `rain_chance1` int(4) NOT NULL DEFAULT 0,
  `rain_chance2` int(4) NOT NULL DEFAULT 0,
  `rain_chance3` int(4) NOT NULL DEFAULT 0,
  `rain_chance4` int(4) NOT NULL DEFAULT 0,
  `rain_duration1` int(4) NOT NULL DEFAULT 0,
  `rain_duration2` int(4) NOT NULL DEFAULT 0,
  `rain_duration3` int(4) NOT NULL DEFAULT 0,
  `rain_duration4` int(4) NOT NULL DEFAULT 0,
  `snow_chance1` int(4) NOT NULL DEFAULT 0,
  `snow_chance2` int(4) NOT NULL DEFAULT 0,
  `snow_chance3` int(4) NOT NULL DEFAULT 0,
  `snow_chance4` int(4) NOT NULL DEFAULT 0,
  `snow_duration1` int(4) NOT NULL DEFAULT 0,
  `snow_duration2` int(4) NOT NULL DEFAULT 0,
  `snow_duration3` int(4) NOT NULL DEFAULT 0,
  `snow_duration4` int(4) NOT NULL DEFAULT 0,
  `gravity` float NOT NULL DEFAULT 0.4,
  `type` int(3) NOT NULL DEFAULT 0,
  `skylock` tinyint(4) NOT NULL DEFAULT 0,
  PRIMARY KEY (`short_name`),
  UNIQUE KEY `zoneidnumber` (`zoneidnumber`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `zone_flags`
--

DROP TABLE IF EXISTS `zone_flags`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `zone_flags` (
  `charID` int(11) NOT NULL DEFAULT 0,
  `zoneID` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`charID`,`zoneID`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `zone_points`
--

DROP TABLE IF EXISTS `zone_points`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `zone_points` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `zone` varchar(32) DEFAULT NULL,
  `version` int(11) NOT NULL DEFAULT 0,
  `number` smallint(4) unsigned NOT NULL DEFAULT 1,
  `y` float NOT NULL DEFAULT 0,
  `x` float NOT NULL DEFAULT 0,
  `z` float NOT NULL DEFAULT 0,
  `heading` float NOT NULL DEFAULT 0,
  `target_y` float NOT NULL DEFAULT 0,
  `target_x` float NOT NULL DEFAULT 0,
  `target_z` float NOT NULL DEFAULT 0,
  `target_heading` float NOT NULL DEFAULT 0,
  `zoneinst` smallint(5) unsigned DEFAULT 0,
  `target_zone_id` int(10) unsigned NOT NULL DEFAULT 0,
  `target_instance` int(10) unsigned NOT NULL DEFAULT 0,
  `buffer` float DEFAULT 0,
  `client_version_mask` int(10) unsigned NOT NULL DEFAULT 4294967295,
  `min_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `max_expansion` tinyint(4) NOT NULL DEFAULT -1,
  `content_flags` varchar(100) DEFAULT NULL,
  `content_flags_disabled` varchar(100) DEFAULT NULL,
  `is_virtual` tinyint(4) NOT NULL DEFAULT 0,
  `height` int(11) NOT NULL DEFAULT 0,
  `width` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `NewIndex` (`number`,`zone`),
  KEY `zone_points_target_idx` (`target_zone_id`) USING BTREE
) ENGINE=MyISAM AUTO_INCREMENT=876 DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `zone_server`
--

DROP TABLE IF EXISTS `zone_server`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `zone_server` (
  `name` varchar(16) NOT NULL DEFAULT '',
  `address` text NOT NULL,
  `port` int(11) NOT NULL DEFAULT 0,
  `player_count` int(11) NOT NULL DEFAULT 0,
  `last_alive` timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  `rain` char(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`name`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `zone_state_dump`
--

DROP TABLE IF EXISTS `zone_state_dump`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `zone_state_dump` (
  `zonename` varchar(16) NOT NULL DEFAULT '',
  `spawn2_count` int(10) unsigned NOT NULL DEFAULT 0,
  `npc_count` int(10) unsigned NOT NULL DEFAULT 0,
  `npcloot_count` int(10) unsigned NOT NULL DEFAULT 0,
  `gmspawntype_count` int(10) unsigned NOT NULL DEFAULT 0,
  `spawn2` mediumblob DEFAULT NULL,
  `npcs` mediumblob DEFAULT NULL,
  `npc_loot` mediumblob DEFAULT NULL,
  `gmspawntype` mediumblob DEFAULT NULL,
  `time` timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  PRIMARY KEY (`zonename`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `zoneserver_auth`
--

DROP TABLE IF EXISTS `zoneserver_auth`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!40101 SET character_set_client = utf8mb4 */;
CREATE TABLE `zoneserver_auth` (
  `host` varchar(30) NOT NULL DEFAULT '',
  `note` text DEFAULT NULL,
  PRIMARY KEY (`host`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_uca1400_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Final view structure for view `vaggroradius`
--

/*!50001 DROP VIEW IF EXISTS `vaggroradius`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8mb4 */;
/*!50001 SET character_set_results     = utf8mb4 */;
/*!50001 SET collation_connection      = utf8mb4_uca1400_ai_ci */;
/*!50001 CREATE ALGORITHM=UNDEFINED */
/*!50013 DEFINER=`root`@`localhost` SQL SECURITY DEFINER */
/*!50001 VIEW `vaggroradius` AS select `nt`.`id` AS `id`,`nt`.`name` AS `name`,`nt`.`aggroradius` AS `aggroradius`,`nt`.`assistradius` AS `assistradius`,`s2`.`x` AS `Spawn2X`,`s2`.`y` AS `Spawn2Y`,`s2`.`z` AS `Spawn2Z`,`sg`.`name` AS `spawngroup_name`,`sg`.`id` AS `Spawngroup_id`,`sg`.`min_x` AS `Spawngroup_minX`,`sg`.`max_x` AS `Spawngroup_maxX`,`sg`.`min_y` AS `Spawngroup_minY`,`sg`.`max_y` AS `Spawngroup_maxY`,`sg`.`dist` AS `Spawngroup_dist`,`sg`.`mindelay` AS `Spawngroup_mindelay`,`sg`.`delay` AS `Spawngroup_delay` from (((`spawn2` `s2` join `spawngroup` `sg` on(`sg`.`id` = `s2`.`spawngroupID`)) join `spawnentry` `se` on(`se`.`spawngroupID` = `sg`.`id`)) join `npc_types` `nt` on(`nt`.`id` = `se`.`npcID`)) where `s2`.`zone` = 'airplane' */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;

--
-- Final view structure for view `vatkdelayzero`
--

/*!50001 DROP VIEW IF EXISTS `vatkdelayzero`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8mb4 */;
/*!50001 SET character_set_results     = utf8mb4 */;
/*!50001 SET collation_connection      = utf8mb4_uca1400_ai_ci */;
/*!50001 CREATE ALGORITHM=UNDEFINED */
/*!50013 DEFINER=`root`@`localhost` SQL SECURITY DEFINER */
/*!50001 VIEW `vatkdelayzero` AS select `nt`.`id` AS `id`,`nt`.`name` AS `name`,`nt`.`level` AS `level`,`nt`.`attack_delay` AS `attack_delay`,`s2`.`zone` AS `zone` from (((`spawn2` `s2` join `spawngroup` `sg` on(`sg`.`id` = `s2`.`spawngroupID`)) join `spawnentry` `se` on(`se`.`spawngroupID` = `sg`.`id`)) join `npc_types` `nt` on(`nt`.`id` = `se`.`npcID`)) where `nt`.`attack_delay` = 0 */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;

--
-- Final view structure for view `vbot_spells`
--

/*!50001 DROP VIEW IF EXISTS `vbot_spells`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8mb4 */;
/*!50001 SET character_set_results     = utf8mb4 */;
/*!50001 SET collation_connection      = utf8mb4_uca1400_ai_ci */;
/*!50001 CREATE ALGORITHM=UNDEFINED */
/*!50013 DEFINER=`root`@`localhost` SQL SECURITY DEFINER */
/*!50001 VIEW `vbot_spells` AS select `nse`.`name` AS `spell_set_name`,`s`.`name` AS `spell_name`,`bse`.`spellid` AS `spellid`,`bse`.`type` AS `type`,`bse`.`minlevel` AS `minlevel`,`bse`.`maxlevel` AS `maxlevel`,`bse`.`manacost` AS `manacost`,`bse`.`recast_delay` AS `recast_delay`,`bse`.`priority` AS `priority`,`bse`.`resist_adjust` AS `resist_adjust`,`bse`.`min_hp` AS `min_hp`,`bse`.`max_hp` AS `max_hp`,`bse`.`bucket_name` AS `bucket_name`,`bse`.`bucket_value` AS `bucket_value`,`bse`.`bucket_comparison` AS `bucket_comparison` from ((`bot_spells_entries` `bse` join `spells` `s` on(`bse`.`spellid` = `s`.`spellid`)) join `npc_spells` `nse` on(`bse`.`npc_spells_id` = `nse`.`id`)) where `bse`.`npc_spells_id` >= 3000 and `nse`.`name` like '%druid%' order by `bse`.`minlevel` */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;

--
-- Final view structure for view `vbotsnpcspells`
--

/*!50001 DROP VIEW IF EXISTS `vbotsnpcspells`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8mb4 */;
/*!50001 SET character_set_results     = utf8mb4 */;
/*!50001 SET collation_connection      = utf8mb4_uca1400_ai_ci */;
/*!50001 CREATE ALGORITHM=UNDEFINED */
/*!50013 DEFINER=`root`@`localhost` SQL SECURITY DEFINER */
/*!50001 VIEW `vbotsnpcspells` AS select `bse`.`id` AS `bse_spellid`,`bse`.`npc_spells_id` AS `npc_spells_id`,`sn`.`name` AS `name`,`bse`.`spellid` AS `spellid`,`bse`.`type` AS `type`,`bse`.`minlevel` AS `minlevel`,`bse`.`maxlevel` AS `maxlevel`,`bse`.`manacost` AS `manacost`,`bse`.`recast_delay` AS `recast_delay`,`bse`.`priority` AS `priority`,`bse`.`resist_adjust` AS `resist_adjust` from (`bot_spells_entries` `bse` join `spells_new` `sn` on(`sn`.`id` = `bse`.`spellid`)) where `bse`.`npc_spells_id` = '3011' order by `bse`.`minlevel` */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;

--
-- Final view structure for view `vemotes`
--

/*!50001 DROP VIEW IF EXISTS `vemotes`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8mb4 */;
/*!50001 SET character_set_results     = utf8mb4 */;
/*!50001 SET collation_connection      = utf8mb4_uca1400_ai_ci */;
/*!50001 CREATE ALGORITHM=UNDEFINED */
/*!50013 DEFINER=`root`@`localhost` SQL SECURITY DEFINER */
/*!50001 VIEW `vemotes` AS select `nt`.`id` AS `id`,`nt`.`name` AS `name`,`nt`.`emoteid` AS `emoteid`,`ne`.`event_` AS `Emote_Event`,`ne`.`type` AS `Event_Type`,`ne`.`text` AS `text`,`s2`.`x` AS `Spawn2X`,`s2`.`y` AS `Spawn2Y`,`s2`.`z` AS `Spawn2Z`,`sg`.`name` AS `spawngroup_name`,`sg`.`id` AS `Spawngroup_id`,`sg`.`min_x` AS `Spawngroup_minX`,`sg`.`max_x` AS `Spawngroup_maxX`,`sg`.`min_y` AS `Spawngroup_minY`,`sg`.`max_y` AS `Spawngroup_maxY`,`sg`.`dist` AS `Spawngroup_dist`,`sg`.`mindelay` AS `Spawngroup_mindelay`,`sg`.`delay` AS `Spawngroup_delay` from ((((`spawn2` `s2` join `spawngroup` `sg` on(`sg`.`id` = `s2`.`spawngroupID`)) join `spawnentry` `se` on(`se`.`spawngroupID` = `sg`.`id`)) join `npc_types` `nt` on(`nt`.`id` = `se`.`npcID`)) join `npc_emotes` `ne` on(`ne`.`emoteid` = `nt`.`emoteid`)) where `s2`.`zone` = 'commons' */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;

--
-- Final view structure for view `vfaction`
--

/*!50001 DROP VIEW IF EXISTS `vfaction`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8mb4 */;
/*!50001 SET character_set_results     = utf8mb4 */;
/*!50001 SET collation_connection      = utf8mb4_uca1400_ai_ci */;
/*!50001 CREATE ALGORITHM=UNDEFINED */
/*!50013 DEFINER=`root`@`localhost` SQL SECURITY DEFINER */
/*!50001 VIEW `vfaction` AS select `nt`.`id` AS `id`,`sg`.`id` AS `sgID`,`nt`.`name` AS `name`,`nt`.`npc_faction_id` AS `npc_faction_id` from (((`spawn2` `s2` join `spawngroup` `sg` on(`sg`.`id` = `s2`.`spawngroupID`)) join `spawnentry` `se` on(`se`.`spawngroupID` = `sg`.`id`)) join `npc_types` `nt` on(`nt`.`id` = `se`.`npcID`)) where `s2`.`zone` = 'greatdivide' */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;

--
-- Final view structure for view `vhp`
--

/*!50001 DROP VIEW IF EXISTS `vhp`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8mb4 */;
/*!50001 SET character_set_results     = utf8mb4 */;
/*!50001 SET collation_connection      = utf8mb4_uca1400_ai_ci */;
/*!50001 CREATE ALGORITHM=UNDEFINED */
/*!50013 DEFINER=`root`@`localhost` SQL SECURITY DEFINER */
/*!50001 VIEW `vhp` AS select `nt`.`id` AS `id`,`nt`.`name` AS `name`,`nt`.`level` AS `level`,`nt`.`hp` AS `hp`,`nt`.`hp_regen_rate` AS `hp_regen_rate` from (((`spawn2` `s2` join `spawngroup` `sg` on(`sg`.`id` = `s2`.`spawngroupID`)) join `spawnentry` `se` on(`se`.`spawngroupID` = `sg`.`id`)) join `npc_types` `nt` on(`nt`.`id` = `se`.`npcID`)) where `s2`.`zone` = 'westwastes' */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;

--
-- Final view structure for view `vloottables`
--

/*!50001 DROP VIEW IF EXISTS `vloottables`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8mb4 */;
/*!50001 SET character_set_results     = utf8mb4 */;
/*!50001 SET collation_connection      = utf8mb4_uca1400_ai_ci */;
/*!50001 CREATE ALGORITHM=UNDEFINED */
/*!50013 DEFINER=`root`@`localhost` SQL SECURITY DEFINER */
/*!50001 VIEW `vloottables` AS select `nt`.`id` AS `id`,`nt`.`name` AS `name`,`nt`.`level` AS `level`,`nt`.`loottable_id` AS `loottable_id` from (((`spawn2` `s2` join `spawngroup` `sg` on(`sg`.`id` = `s2`.`spawngroupID`)) join `spawnentry` `se` on(`se`.`spawngroupID` = `sg`.`id`)) join `npc_types` `nt` on(`nt`.`id` = `se`.`npcID`)) where `s2`.`zone` = 'skyshrine' */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;

--
-- Final view structure for view `vrespawn`
--

/*!50001 DROP VIEW IF EXISTS `vrespawn`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8mb4 */;
/*!50001 SET character_set_results     = utf8mb4 */;
/*!50001 SET collation_connection      = utf8mb4_uca1400_ai_ci */;
/*!50001 CREATE ALGORITHM=UNDEFINED */
/*!50013 DEFINER=`root`@`localhost` SQL SECURITY DEFINER */
/*!50001 VIEW `vrespawn` AS select `nt`.`id` AS `id`,`nt`.`name` AS `name`,`nt`.`level` AS `level`,`s2`.`respawntime` AS `respawntime`,`s2`.`x` AS `Spawn2X`,`s2`.`y` AS `Spawn2Y`,`s2`.`z` AS `Spawn2Z`,`sg`.`name` AS `spawngroup_name`,`sg`.`id` AS `Spawngroup_id`,`sg`.`min_x` AS `Spawngroup_minX`,`sg`.`max_x` AS `Spawngroup_maxX`,`sg`.`min_y` AS `Spawngroup_minY`,`sg`.`max_y` AS `Spawngroup_maxY`,`sg`.`dist` AS `Spawngroup_dist`,`sg`.`mindelay` AS `Spawngroup_mindelay`,`sg`.`delay` AS `Spawngroup_delay` from (((`spawn2` `s2` join `spawngroup` `sg` on(`sg`.`id` = `s2`.`spawngroupID`)) join `spawnentry` `se` on(`se`.`spawngroupID` = `sg`.`id`)) join `npc_types` `nt` on(`nt`.`id` = `se`.`npcID`)) where `s2`.`zone` = 'soldungb' */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;

--
-- Final view structure for view `vspawncondi`
--

/*!50001 DROP VIEW IF EXISTS `vspawncondi`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8mb4 */;
/*!50001 SET character_set_results     = utf8mb4 */;
/*!50001 SET collation_connection      = utf8mb4_uca1400_ai_ci */;
/*!50001 CREATE ALGORITHM=UNDEFINED */
/*!50013 DEFINER=`root`@`localhost` SQL SECURITY DEFINER */
/*!50001 VIEW `vspawncondi` AS select `nt`.`id` AS `id`,`nt`.`name` AS `name`,`nt`.`level` AS `level`,`s2`.`zone` AS `zone`,`sg`.`name` AS `spawngroup_name`,`sg`.`id` AS `Spawngroup_id`,`s2`.`_condition` AS `_condition` from (((`spawn2` `s2` join `spawngroup` `sg` on(`sg`.`id` = `s2`.`spawngroupID`)) join `spawnentry` `se` on(`se`.`spawngroupID` = `sg`.`id`)) join `npc_types` `nt` on(`nt`.`id` = `se`.`npcID`)) where `s2`.`_condition` = 1 and `s2`.`zone` = 'thurgadina' */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;

--
-- Final view structure for view `vspawnlocs2`
--

/*!50001 DROP VIEW IF EXISTS `vspawnlocs2`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8mb4 */;
/*!50001 SET character_set_results     = utf8mb4 */;
/*!50001 SET collation_connection      = utf8mb4_uca1400_ai_ci */;
/*!50001 CREATE ALGORITHM=UNDEFINED */
/*!50013 DEFINER=`root`@`localhost` SQL SECURITY DEFINER */
/*!50001 VIEW `vspawnlocs2` AS select `nt`.`id` AS `id`,`nt`.`name` AS `name`,`nt`.`npc_aggro` AS `CanAggro`,`s2`.`_condition` AS `_condition`,`s2`.`zone` AS `zone`,`s2`.`pathgrid` AS `pathgrid`,`s2`.`x` AS `Spawn2X`,`s2`.`y` AS `Spawn2Y`,`s2`.`z` AS `Spawn2Z`,`s2`.`heading` AS `Spawn2H`,`s2`.`respawntime` AS `Respawn`,`sg`.`name` AS `spawngroup_name`,`sg`.`id` AS `Spawngroup_id`,`sg`.`min_x` AS `Spawngroup_minX`,`sg`.`max_x` AS `Spawngroup_maxX`,`sg`.`min_y` AS `Spawngroup_minY`,`sg`.`max_y` AS `Spawngroup_maxY`,`sg`.`dist` AS `Spawngroup_dist` from (((`spawn2` `s2` join `spawngroup` `sg` on(`sg`.`id` = `s2`.`spawngroupID`)) join `spawnentry` `se` on(`se`.`spawngroupID` = `sg`.`id`)) join `npc_types` `nt` on(`nt`.`id` = `se`.`npcID`)) where `s2`.`zone` = 'growthplane' */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;

--
-- Final view structure for view `vspecialattacks`
--

/*!50001 DROP VIEW IF EXISTS `vspecialattacks`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8mb4 */;
/*!50001 SET character_set_results     = utf8mb4 */;
/*!50001 SET collation_connection      = utf8mb4_uca1400_ai_ci */;
/*!50001 CREATE ALGORITHM=UNDEFINED */
/*!50013 DEFINER=`root`@`localhost` SQL SECURITY DEFINER */
/*!50001 VIEW `vspecialattacks` AS select `nt`.`id` AS `id`,`nt`.`name` AS `name`,`nt`.`level` AS `level`,`nt`.`hp` AS `hp`,`nt`.`special_abilities` AS `special_abilities`,`nt`.`hp_regen_rate` AS `hp_regen_rate`,`nt`.`mana_regen_rate` AS `mana_regen_rate`,`nt`.`aggroradius` AS `aggroradius`,`nt`.`assistradius` AS `assistradius`,`nt`.`see_invis` AS `see_invis`,`nt`.`see_invis_undead` AS `see_invis_undead`,`nt`.`see_hide` AS `see_hide`,`nt`.`emoteid` AS `emoteid`,`nt`.`attack_count` AS `attack_count`,`s2`.`x` AS `Spawn2X`,`s2`.`y` AS `Spawn2Y`,`s2`.`z` AS `Spawn2Z`,`sg`.`name` AS `spawngroup_name`,`sg`.`id` AS `Spawngroup_id`,`sg`.`min_x` AS `Spawngroup_minX`,`sg`.`max_x` AS `Spawngroup_maxX`,`sg`.`min_y` AS `Spawngroup_minY`,`sg`.`max_y` AS `Spawngroup_maxY`,`sg`.`dist` AS `Spawngroup_dist`,`sg`.`mindelay` AS `Spawngroup_mindelay`,`sg`.`delay` AS `Spawngroup_delay` from (((`spawn2` `s2` join `spawngroup` `sg` on(`sg`.`id` = `s2`.`spawngroupID`)) join `spawnentry` `se` on(`se`.`spawngroupID` = `sg`.`id`)) join `npc_types` `nt` on(`nt`.`id` = `se`.`npcID`)) where `s2`.`zone` = 'veeshan' */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;

--
-- Final view structure for view `vspellsets`
--

/*!50001 DROP VIEW IF EXISTS `vspellsets`*/;
/*!50001 SET @saved_cs_client          = @@character_set_client */;
/*!50001 SET @saved_cs_results         = @@character_set_results */;
/*!50001 SET @saved_col_connection     = @@collation_connection */;
/*!50001 SET character_set_client      = utf8mb4 */;
/*!50001 SET character_set_results     = utf8mb4 */;
/*!50001 SET collation_connection      = utf8mb4_uca1400_ai_ci */;
/*!50001 CREATE ALGORITHM=UNDEFINED */
/*!50013 DEFINER=`root`@`localhost` SQL SECURITY DEFINER */
/*!50001 VIEW `vspellsets` AS select `nt`.`id` AS `id`,`nt`.`name` AS `name`,`nt`.`class` AS `class`,`nt`.`npc_spells_id` AS `npc_spells_id`,`nt`.`level` AS `level`,`s2`.`x` AS `Spawn2X`,`s2`.`y` AS `Spawn2Y`,`s2`.`z` AS `Spawn2Z`,`sg`.`name` AS `spawngroup_name`,`sg`.`id` AS `Spawngroup_id`,`sg`.`min_x` AS `Spawngroup_minX`,`sg`.`max_x` AS `Spawngroup_maxX`,`sg`.`min_y` AS `Spawngroup_minY`,`sg`.`max_y` AS `Spawngroup_maxY`,`sg`.`dist` AS `Spawngroup_dist`,`sg`.`mindelay` AS `Spawngroup_mindelay`,`sg`.`delay` AS `Spawngroup_delay` from (((`spawn2` `s2` join `spawngroup` `sg` on(`sg`.`id` = `s2`.`spawngroupID`)) join `spawnentry` `se` on(`se`.`spawngroupID` = `sg`.`id`)) join `npc_types` `nt` on(`nt`.`id` = `se`.`npcID`)) where `s2`.`zone` = 'qrg' */;
/*!50001 SET character_set_client      = @saved_cs_client */;
/*!50001 SET character_set_results     = @saved_cs_results */;
/*!50001 SET collation_connection      = @saved_col_connection */;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*M!100616 SET NOTE_VERBOSITY=@OLD_NOTE_VERBOSITY */;

-- Dump completed on 2025-03-16 12:44:29
