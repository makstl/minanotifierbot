using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
namespace MinaNotifierBot.MinaExplorer
{
    public class Block
    {
        public int blockHeight { get; set; }
        public bool canonical { get; set; }
		public string creator { get; set; }
		public Account creatorAccount { get; set; }
        public string dateTime { get; set; }
        public ProtocolState protocolState { get; set; }
        public string receivedTime { get; set; }
        //public List<object> snarkJobs { get; set; }
        public string stateHash { get; set; }
        public string stateHashField { get; set; }
        public Transactions transactions { get; set; }
        public WinnerAccount winnerAccount { get; set; }
        public bool last { get; set; }
    }

    public class BlockchainState
    {
        public long date { get; set; }
        public string snarkedLedgerHash { get; set; }
        public string stagedLedgerHash { get; set; }
        public long utcDate { get; set; }
    }    

    public class ConsensusState
    {
        public int blockHeight { get; set; }
        public int blockchainLength { get; set; }
        public int epoch { get; set; }
        public int epochCount { get; set; }
        public bool hasAncestorInSameCheckpointWindow { get; set; }
        public string lastVrfOutput { get; set; }
        public int minWindowDensity { get; set; }
        public NextEpochData nextEpochData { get; set; }
        public int slot { get; set; }
        public int slotSinceGenesis { get; set; }
        public StakingEpochData stakingEpochData { get; set; }
        public long totalCurrency { get; set; }
    }

    public class FeePayer
    {
        public string publicKey { get; set; }
        public int token { get; set; }
    }

    public class FeeTransfer
    {
        public long fee { get; set; }
        public string recipient { get; set; }
        public string type { get; set; }
    }

    public class FromAccount
    {
        public string publicKey { get; set; }
        public int token { get; set; }
    }

    public class Ledger
    {
        public string hash { get; set; }
        public long totalCurrency { get; set; }
    }

    public class NextEpochData
    {
        public int epochLength { get; set; }
        public Ledger ledger { get; set; }
        public string lockCheckpoint { get; set; }
        public string seed { get; set; }
        public string startCheckpoint { get; set; }
    }

    public class ProtocolState
    {
        public BlockchainState blockchainState { get; set; }
        public ConsensusState consensusState { get; set; }
        public string previousStateHash { get; set; }
    }

    public class Receiver
    {
        public string publicKey { get; set; }
    }

    public class BlocksResult
    {
        public List<Block> blocks { get; set; }
    }
    
    public class StakingEpochData
    {
        public int epochLength { get; set; }
        public Ledger ledger { get; set; }
        public string lockCheckpoint { get; set; }
        public string seed { get; set; }
        public string startCheckpoint { get; set; }
    }

    public class ToAccount
    {
        public string publicKey { get; set; }
        public int token { get; set; }
    }

    public class Transactions
    {
        public long coinbase { get; set; }
        public Account coinbaseReceiverAccount { get; set; }
        public List<FeeTransfer> feeTransfer { get; set; }
        public List<UserCommand> userCommands { get; set; }
    }

    public class UserCommand
    {
        public ulong amount { get; set; }
        public int blockHeight { get; set; }
        public string blockStateHash { get; set; }
        public string dateTime { get; set; }
        public ulong fee { get; set; }
        public FeePayer feePayer { get; set; }
        public int feeToken { get; set; }
        public string from { get; set; }
        public FromAccount fromAccount { get; set; }
        public string hash { get; set; }
        public string id { get; set; }
        public bool isDelegation { get; set; }
        public string kind { get; set; }
        public string memo { get; set; }
        public int nonce { get; set; }
        public Receiver receiver { get; set; }
        public Account source { get; set; }
        public string to { get; set; }
        public ToAccount toAccount { get; set; }
        public int token { get; set; }
    }

    public class WinnerAccount
    {
        public Balance balance { get; set; }
        public string publicKey { get; set; }
    }
}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
